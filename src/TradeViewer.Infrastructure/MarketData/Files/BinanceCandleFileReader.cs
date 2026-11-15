using System.Globalization;
using TradeViewer.Application.Abstractions.MarketData;
using TradeViewer.Application.Abstractions.Messaging;
using TradeViewer.Domain.MarketData;

namespace TradeViewer.Infrastructure.MarketData.Files;

/// <summary>
/// The Binance kline format (docs/knowledge/downloader-interface.md): comma separated, columns
/// 0 open time, 1 open, 2 high, 3 low, 4 close, 5 volume; the other columns are not read.
/// Open times are milliseconds or, from 2025, microseconds; one file can mix both, so the unit is
/// detected per value. A header row (futures files) is skipped.
/// </summary>
public sealed class BinanceCandleFileReader : ICandleFileReader
{
    private const int ColumnsRead = 6;

    /// <summary>An open time from this value on is in microseconds (as milliseconds it would be the year 5138).</summary>
    private const long MicrosecondsFrom = 100_000_000_000_000;

    public Result<IReadOnlyList<Candle>> Read(string path, CandleFile file)
    {
        var candles = new List<Candle>();
        long? previousTime = null;
        var lineNumber = 0;
        var blankLine = 0;

        try
        {
            foreach (var line in File.ReadLines(path))
            {
                lineNumber++;

                if (line.Length == 0)
                {
                    // Empty lines are fine at the end of the file only.
                    blankLine = blankLine == 0 ? lineNumber : blankLine;
                    continue;
                }

                if (blankLine != 0)
                {
                    return Invalid(file, blankLine, "the line is empty");
                }

                if (lineNumber == 1 && !char.IsAsciiDigit(line[0]))
                {
                    continue; // header row
                }

                var problem = ParseRow(line, out var candle) ?? CandleFileRules.CheckRow(candle, previousTime, file);
                if (problem != null)
                {
                    return Invalid(file, lineNumber, problem);
                }

                candles.Add(candle);
                previousTime = candle.Time;
            }
        }
        catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException)
        {
            return Error.NotFound("candleFile.notFound", "The file " + file.Name + " does not exist.");
        }

        return Result.Success<IReadOnlyList<Candle>>(candles);
    }

    /// <returns>Null when the row could be read, otherwise what is wrong with it.</returns>
    private static string? ParseRow(string line, out Candle candle)
    {
        candle = default;

        var fields = new double[ColumnsRead];
        var rest = line.AsSpan();
        long time = 0;

        for (var column = 0; column < ColumnsRead; column++)
        {
            var comma = rest.IndexOf(',');
            if (comma < 0 && column < ColumnsRead - 1)
            {
                return "the row has fewer than " + ColumnsRead + " columns";
            }

            var field = comma < 0 ? rest : rest.Slice(0, comma);
            rest = comma < 0 ? ReadOnlySpan<char>.Empty : rest.Slice(comma + 1);

            if (column == 0)
            {
                if (!long.TryParse(field, NumberStyles.None, CultureInfo.InvariantCulture, out time))
                {
                    return "the open time is not a number";
                }
            }
            else if (!double.TryParse(field, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out fields[column]))
            {
                return "column " + column + " is not a number";
            }
        }

        var unitsPerSecond = time >= MicrosecondsFrom ? 1_000_000 : 1_000;
        if (time % unitsPerSecond != 0)
        {
            return "the open time is not a whole second";
        }

        candle = new Candle(time / unitsPerSecond, fields[1], fields[2], fields[3], fields[4], fields[5]);
        return null;
    }

    private static Error Invalid(CandleFile file, int line, string problem)
    {
        return Error.InvalidData("candleFile.invalid", file.Name + ", line " + line + ": " + problem);
    }
}

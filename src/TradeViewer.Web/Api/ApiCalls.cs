using System.Net.Http.Json;
using System.Text.Json;

namespace TradeViewer.Web.Api;

/// <summary>The outcome of a call that returns a value: the value, or the message to show.</summary>
public sealed record ApiResult<T>(T? Value, string? Error)
    where T : class;

/// <summary>What all API clients share: a failed call becomes the message of its problem details (RFC 9457), never an exception.</summary>
public static class ApiCalls
{
    private const string Unreachable = "The server is not reachable.";

    /// <returns>Null when the call worked, otherwise the message to show.</returns>
    public static async Task<string?> SendAsync(Func<Task<HttpResponseMessage>> send)
    {
        try
        {
            using var response = await send();
            return response.IsSuccessStatusCode ? null : await ReadProblemAsync(response);
        }
        catch (HttpRequestException)
        {
            return Unreachable;
        }
    }

    public static async Task<ApiResult<T>> SendAsync<T>(Func<Task<HttpResponseMessage>> send)
        where T : class
    {
        try
        {
            using var response = await send();
            if (!response.IsSuccessStatusCode)
            {
                return new ApiResult<T>(null, await ReadProblemAsync(response));
            }

            var value = await response.Content.ReadFromJsonAsync<T>();
            return value == null ? new ApiResult<T>(null, "The server answered without content.") : new ApiResult<T>(value, null);
        }
        catch (HttpRequestException)
        {
            return new ApiResult<T>(null, Unreachable);
        }
        catch (JsonException)
        {
            return new ApiResult<T>(null, "The answer of the server could not be read.");
        }
    }

    /// <summary>The message of a failed answer: its problem details, or the status.</summary>
    public static async Task<string> ReadProblemAsync(HttpResponseMessage response)
    {
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = document.RootElement;

            // A validation problem names the fields; their messages say more than the summary.
            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                var messages = new List<string>();
                foreach (var field in errors.EnumerateObject())
                {
                    foreach (var message in field.Value.EnumerateArray())
                    {
                        messages.Add(message.GetString() ?? string.Empty);
                    }
                }

                if (messages.Count > 0)
                {
                    return string.Join(" ", messages);
                }
            }

            if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
            {
                return detail.GetString()!;
            }
        }
        catch (JsonException)
        {
            // no problem details: the status has to do
        }

        return "The request failed (" + (int)response.StatusCode + ").";
    }
}

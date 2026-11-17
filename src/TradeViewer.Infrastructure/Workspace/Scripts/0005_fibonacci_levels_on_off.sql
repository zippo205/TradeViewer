-- Fibonacci levels can be switched on and off (DR4): each level is stored as {"ratio", "visible"}. Plain
-- numbers, as script 0004 wrote them, stay readable as levels that are shown. The two default presets get the
-- levels that TradingView offers switched off, but only where the user has not changed them.
UPDATE fibonacci_presets
SET levels = '[{"ratio":0,"visible":true},{"ratio":0.236,"visible":true},{"ratio":0.382,"visible":true},{"ratio":0.5,"visible":true},{"ratio":0.618,"visible":true},{"ratio":0.786,"visible":true},{"ratio":1,"visible":true},{"ratio":1.272,"visible":false},{"ratio":1.414,"visible":false},{"ratio":1.618,"visible":false},{"ratio":2,"visible":false},{"ratio":2.618,"visible":false}]',
    updated_at = '2026-11-17T00:00:00Z'
WHERE id = '6c0f3b1e-5d2a-4c1b-9a7e-000000000001' AND levels = '[0,0.236,0.382,0.5,0.618,0.786,1]';

UPDATE fibonacci_presets
SET levels = '[{"ratio":0,"visible":false},{"ratio":0.382,"visible":false},{"ratio":0.5,"visible":false},{"ratio":0.618,"visible":false},{"ratio":0.786,"visible":false},{"ratio":1,"visible":true},{"ratio":1.272,"visible":true},{"ratio":1.618,"visible":true},{"ratio":2,"visible":true},{"ratio":2.618,"visible":true},{"ratio":3.618,"visible":true},{"ratio":4.236,"visible":false}]',
    updated_at = '2026-11-17T00:00:00Z'
WHERE id = '6c0f3b1e-5d2a-4c1b-9a7e-000000000002' AND levels = '[1,1.272,1.618,2,2.618,3.618]';

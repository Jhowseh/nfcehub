namespace NFCeHub.Core.Tests;

public static class AccessKeys
{
    public static string Make(string model, int series, long number, string yearMonth = "2610")
    {
        var body = "25" + yearMonth + "11222333000181" + model + series.ToString("000") + number.ToString("000000000") +
                   "1" + "12345678";
        int total = 0, weight = 2;
        for (var i = body.Length - 1; i >= 0; i--)
        {
            total += (body[i] - '0') * weight;
            weight = weight == 9 ? 2 : weight + 1;
        }
        var remainder = total % 11;
        return body + (remainder < 2 ? 0 : 11 - remainder);
    }
}

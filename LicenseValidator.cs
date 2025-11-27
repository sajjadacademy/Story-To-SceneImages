using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public class LicenseValidator
{
    private readonly string _publicKey;

    public LicenseValidator()
    {
        _publicKey = File.ReadAllText("public.key"); // embed as resource if needed
    }

    

    public bool Validate(string file, out string message)
    {
        message = "";

        if (!File.Exists(file))
        {
            message = "License file not found.";
            return false;
        }

        var json = File.ReadAllText(file);

        var root = JsonDocument.Parse(json).RootElement;

        var dataJson = root.GetProperty("data").GetRawText();
        var signature = root.GetProperty("signature").GetString();

        if (string.IsNullOrWhiteSpace(signature))
        {
            message = "License signature missing.";
            return false;
        }

        bool ok = Verify(dataJson, signature);

        if (!ok)
        {
            message = "Invalid or tampered license.";
            return false;
        }

        // Validate fields
        var lic = JsonSerializer.Deserialize<LicenseInfo>(dataJson);

        if (lic == null)
        {
            message = "License data is invalid.";
            return false;
        }

        if (lic.ExpiryDate < DateTime.UtcNow)
        {
            message = "License expired.";
            return false;
        }

        message = $"Valid License. User: {lic.UserName}, Plan: {lic.Plan}";
        return true;
    }

    private bool Verify(string json, string signature)
    {
        byte[] data = Encoding.UTF8.GetBytes(json);
        byte[] sig = Convert.FromBase64String(signature);

        using var rsa = RSA.Create();
        rsa.ImportRSAPublicKey(Convert.FromBase64String(_publicKey), out _);

        return rsa.VerifyData(data, sig,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
    }

    public static bool ValidateText(string text, out string message)
    {
        try
        {
            // Save temp file
            string tempPath = Path.Combine(Path.GetTempPath(), "temp_license.lic");
            File.WriteAllText(tempPath, text);

            var validator = new LicenseValidator();
            return validator.Validate(tempPath, out message);
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return false;
        }
    }
}

public class LicenseInfo
{
    public string LicenseKey { get; set; } = "";
    public string UserName { get; set; } = "";
    public string Plan { get; set; } = "pro";
    public DateTime ExpiryDate { get; set; }
}

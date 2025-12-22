using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Serilog;

namespace TLScope.Utilities;

/// <summary>
/// Cryptographic utilities for SSH key handling, certificate generation, and peer authentication
/// </summary>
public static class CryptoUtility
{
    /// <summary>
    /// Generate a random challenge for peer verification (32 bytes, base64-encoded)
    /// </summary>
    public static string GenerateChallenge()
    {
        var random = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(random);
    }

    /// <summary>
    /// Sign a challenge with SSH private key
    /// </summary>
    /// <param name="challenge">Challenge string to sign</param>
    /// <param name="privateKeyPath">Path to SSH private key file</param>
    /// <returns>Base64-encoded signature</returns>
    public static string SignChallenge(string challenge, string privateKeyPath)
    {
        try
        {
            using var rsa = LoadRSAPrivateKeyFromSSH(privateKeyPath);
            var data = Encoding.UTF8.GetBytes(challenge);
            var signature = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return Convert.ToBase64String(signature);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to sign challenge");
            throw;
        }
    }

    /// <summary>
    /// Verify signature against SSH public key
    /// </summary>
    /// <param name="challenge">Original challenge string</param>
    /// <param name="signature">Base64-encoded signature</param>
    /// <param name="sshPublicKey">SSH public key in "ssh-rsa AAAAB3..." format</param>
    /// <returns>True if signature is valid</returns>
    public static bool VerifySignature(string challenge, string signature, string sshPublicKey)
    {
        try
        {
            using var rsa = LoadRSAPublicKeyFromSSH(sshPublicKey);
            var data = Encoding.UTF8.GetBytes(challenge);
            var signatureBytes = Convert.FromBase64String(signature);

            return rsa.VerifyData(data, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to verify signature");
            return false;
        }
    }

    /// <summary>
    /// Generate X.509 certificate from SSH private key
    /// </summary>
    /// <param name="username">Username for certificate CN</param>
    /// <param name="privateKeyPath">Path to SSH private key</param>
    /// <returns>Self-signed X509 certificate</returns>
    public static X509Certificate2 GenerateCertificateFromSSHKey(string username, string privateKeyPath)
    {
        try
        {
            using var rsa = LoadRSAPrivateKeyFromSSH(privateKeyPath);

            // Create certificate request
            var request = new CertificateRequest(
                new X500DistinguishedName($"CN={username}@tlscope"),
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1
            );

            // Add subject alternative names
            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName("tlscope.local");
            sanBuilder.AddDnsName("localhost");
            request.CertificateExtensions.Add(sanBuilder.Build());

            // Add key usage
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                    critical: true
                )
            );

            // Self-sign certificate (5-year validity)
            var certificate = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddYears(5)
            );

            Log.Information($"Generated certificate for {username}");
            return certificate;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to generate certificate from SSH key");
            throw;
        }
    }

    /// <summary>
    /// Verify that a certificate's public key matches an SSH public key
    /// </summary>
    /// <param name="certificate">X.509 certificate to check</param>
    /// <param name="sshPublicKey">SSH public key in "ssh-rsa AAAAB3..." format</param>
    /// <returns>True if keys match</returns>
    public static bool VerifyCertificateMatchesSSHKey(X509Certificate2 certificate, string sshPublicKey)
    {
        try
        {
            var certRsa = certificate.GetRSAPublicKey();
            if (certRsa == null)
                return false;

            using var sshRsa = LoadRSAPublicKeyFromSSH(sshPublicKey);

            var certParams = certRsa.ExportParameters(false);
            var sshParams = sshRsa.ExportParameters(false);

            // Compare modulus and exponent
            if (certParams.Modulus == null || certParams.Exponent == null ||
                sshParams.Modulus == null || sshParams.Exponent == null)
                return false;

            return certParams.Modulus.SequenceEqual(sshParams.Modulus) &&
                   certParams.Exponent.SequenceEqual(sshParams.Exponent);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to verify certificate against SSH key");
            return false;
        }
    }

    /// <summary>
    /// Load RSA private key from SSH private key file
    /// Supports OpenSSH format and PEM format
    /// </summary>
    /// <param name="privateKeyPath">Path to private key file</param>
    /// <returns>RSA instance</returns>
    public static RSA LoadRSAPrivateKeyFromSSH(string privateKeyPath)
    {
        var keyData = File.ReadAllText(privateKeyPath);

        // Try PEM format first (-----BEGIN RSA PRIVATE KEY-----)
        if (keyData.Contains("BEGIN RSA PRIVATE KEY"))
        {
            return LoadRSAPrivateKeyFromPEM(keyData);
        }
        // Try PKCS#8 PEM format (-----BEGIN PRIVATE KEY-----)
        else if (keyData.Contains("BEGIN PRIVATE KEY"))
        {
            return LoadRSAPrivateKeyFromPKCS8PEM(keyData);
        }
        // Try OpenSSH format (-----BEGIN OPENSSH PRIVATE KEY-----)
        else if (keyData.Contains("BEGIN OPENSSH PRIVATE KEY"))
        {
            return LoadRSAPrivateKeyFromOpenSSH(keyData);
        }
        else
        {
            throw new NotSupportedException("Unsupported private key format. Supported formats: RSA PEM, PKCS#8 PEM, OpenSSH");
        }
    }

    /// <summary>
    /// Load RSA public key from SSH public key string
    /// Supports "ssh-rsa AAAAB3..." format
    /// </summary>
    /// <param name="sshPublicKey">SSH public key string</param>
    /// <returns>RSA instance</returns>
    public static RSA LoadRSAPublicKeyFromSSH(string sshPublicKey)
    {
        // Format: "ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAAB... user@host"
        var parts = sshPublicKey.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
            throw new FormatException("Invalid SSH public key format");

        var keyType = parts[0];
        if (keyType != "ssh-rsa")
            throw new NotSupportedException($"Unsupported key type: {keyType}. Only RSA keys are currently supported.");

        var base64Data = parts[1];
        var keyBytes = Convert.FromBase64String(base64Data);

        return ParseSSHRSAPublicKey(keyBytes);
    }

    /// <summary>
    /// Parse SSH RSA public key from binary data
    /// SSH key format: [4-byte length][key type string][4-byte length][exponent][4-byte length][modulus]
    /// </summary>
    private static RSA ParseSSHRSAPublicKey(byte[] keyBytes)
    {
        using var stream = new MemoryStream(keyBytes);
        using var reader = new BinaryReader(stream);

        // Read key type
        var keyTypeLength = ReadInt32BigEndian(reader);
        var keyType = Encoding.ASCII.GetString(reader.ReadBytes(keyTypeLength));

        if (keyType != "ssh-rsa")
            throw new FormatException($"Expected ssh-rsa, got {keyType}");

        // Read exponent
        var exponentLength = ReadInt32BigEndian(reader);
        var exponent = reader.ReadBytes(exponentLength);

        // Read modulus
        var modulusLength = ReadInt32BigEndian(reader);
        var modulus = reader.ReadBytes(modulusLength);

        // Remove leading zero byte if present (used for sign bit in SSH format)
        if (exponent[0] == 0)
            exponent = exponent.Skip(1).ToArray();
        if (modulus[0] == 0)
            modulus = modulus.Skip(1).ToArray();

        // Create RSA parameters
        var rsaParams = new RSAParameters
        {
            Exponent = exponent,
            Modulus = modulus
        };

        var rsa = RSA.Create();
        rsa.ImportParameters(rsaParams);
        return rsa;
    }

    /// <summary>
    /// Load RSA private key from traditional PEM format
    /// </summary>
    private static RSA LoadRSAPrivateKeyFromPEM(string pemData)
    {
        // Extract base64 data between BEGIN and END markers
        var base64 = ExtractPEMBase64(pemData, "RSA PRIVATE KEY");
        var keyBytes = Convert.FromBase64String(base64);

        var rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(keyBytes, out _);
        return rsa;
    }

    /// <summary>
    /// Load RSA private key from PKCS#8 PEM format
    /// </summary>
    private static RSA LoadRSAPrivateKeyFromPKCS8PEM(string pemData)
    {
        var base64 = ExtractPEMBase64(pemData, "PRIVATE KEY");
        var keyBytes = Convert.FromBase64String(base64);

        var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(keyBytes, out _);
        return rsa;
    }

    /// <summary>
    /// Load RSA private key from OpenSSH format
    /// This is more complex and requires parsing the OpenSSH binary format
    /// </summary>
    private static RSA LoadRSAPrivateKeyFromOpenSSH(string opensshData)
    {
        var base64 = ExtractPEMBase64(opensshData, "OPENSSH PRIVATE KEY");
        var keyBytes = Convert.FromBase64String(base64);

        using var stream = new MemoryStream(keyBytes);
        using var reader = new BinaryReader(stream);

        // OpenSSH format header
        var magic = Encoding.ASCII.GetString(reader.ReadBytes(15));
        if (magic != "openssh-key-v1\0")
            throw new FormatException("Invalid OpenSSH key format");

        // Read cipher name (should be "none" for unencrypted keys)
        var cipherLength = ReadInt32BigEndian(reader);
        var cipher = Encoding.ASCII.GetString(reader.ReadBytes(cipherLength));

        // Read KDF name
        var kdfLength = ReadInt32BigEndian(reader);
        var kdf = Encoding.ASCII.GetString(reader.ReadBytes(kdfLength));

        // Read KDF options length
        var kdfOptionsLength = ReadInt32BigEndian(reader);
        reader.ReadBytes(kdfOptionsLength); // Skip KDF options

        // Number of keys
        var numberOfKeys = ReadInt32BigEndian(reader);
        if (numberOfKeys != 1)
            throw new NotSupportedException("Multi-key OpenSSH files are not supported");

        // Public key length
        var publicKeyLength = ReadInt32BigEndian(reader);
        reader.ReadBytes(publicKeyLength); // Skip public key

        // Private key length
        var privateKeyLength = ReadInt32BigEndian(reader);
        var privateKeyBytes = reader.ReadBytes(privateKeyLength);

        if (cipher != "none")
            throw new NotSupportedException("Encrypted OpenSSH keys are not yet supported. Please decrypt your key or use an unencrypted key.");

        // Parse private key section
        return ParseOpenSSHPrivateKeySection(privateKeyBytes);
    }

    /// <summary>
    /// Parse the private key section of an OpenSSH key
    /// </summary>
    private static RSA ParseOpenSSHPrivateKeySection(byte[] privateKeyBytes)
    {
        using var stream = new MemoryStream(privateKeyBytes);
        using var reader = new BinaryReader(stream);

        // Check bytes (should be repeated)
        var check1 = ReadInt32BigEndian(reader);
        var check2 = ReadInt32BigEndian(reader);

        // Read key type
        var keyTypeLength = ReadInt32BigEndian(reader);
        var keyType = Encoding.ASCII.GetString(reader.ReadBytes(keyTypeLength));

        if (keyType != "ssh-rsa")
            throw new NotSupportedException($"Unsupported key type: {keyType}");

        // Read public key components (modulus, exponent)
        var nLength = ReadInt32BigEndian(reader);
        var n = reader.ReadBytes(nLength);
        if (n[0] == 0) n = n.Skip(1).ToArray();

        var eLength = ReadInt32BigEndian(reader);
        var e = reader.ReadBytes(eLength);
        if (e[0] == 0) e = e.Skip(1).ToArray();

        // Read private key components (d, iqmp, p, q)
        var dLength = ReadInt32BigEndian(reader);
        var d = reader.ReadBytes(dLength);
        if (d[0] == 0) d = d.Skip(1).ToArray();

        var iqmpLength = ReadInt32BigEndian(reader);
        var iqmp = reader.ReadBytes(iqmpLength);
        if (iqmp[0] == 0) iqmp = iqmp.Skip(1).ToArray();

        var pLength = ReadInt32BigEndian(reader);
        var p = reader.ReadBytes(pLength);
        if (p[0] == 0) p = p.Skip(1).ToArray();

        var qLength = ReadInt32BigEndian(reader);
        var q = reader.ReadBytes(qLength);
        if (q[0] == 0) q = q.Skip(1).ToArray();

        // Create RSA parameters
        var rsaParams = new RSAParameters
        {
            Modulus = n,
            Exponent = e,
            D = d,
            P = p,
            Q = q,
            InverseQ = iqmp,
            DP = CalculateDPFromRSAParams(d, p),
            DQ = CalculateDQFromRSAParams(d, q)
        };

        var rsa = RSA.Create();
        rsa.ImportParameters(rsaParams);
        return rsa;
    }

    /// <summary>
    /// Calculate DP = d mod (p-1)
    /// </summary>
    private static byte[] CalculateDPFromRSAParams(byte[] d, byte[] p)
    {
        var dBig = new System.Numerics.BigInteger(d, isUnsigned: true, isBigEndian: true);
        var pBig = new System.Numerics.BigInteger(p, isUnsigned: true, isBigEndian: true);
        var dp = dBig % (pBig - 1);
        return dp.ToByteArray(isUnsigned: true, isBigEndian: true);
    }

    /// <summary>
    /// Calculate DQ = d mod (q-1)
    /// </summary>
    private static byte[] CalculateDQFromRSAParams(byte[] d, byte[] q)
    {
        var dBig = new System.Numerics.BigInteger(d, isUnsigned: true, isBigEndian: true);
        var qBig = new System.Numerics.BigInteger(q, isUnsigned: true, isBigEndian: true);
        var dq = dBig % (qBig - 1);
        return dq.ToByteArray(isUnsigned: true, isBigEndian: true);
    }

    /// <summary>
    /// Extract base64 data from PEM format
    /// </summary>
    private static string ExtractPEMBase64(string pemData, string keyType)
    {
        var beginMarker = $"-----BEGIN {keyType}-----";
        var endMarker = $"-----END {keyType}-----";

        var startIndex = pemData.IndexOf(beginMarker);
        var endIndex = pemData.IndexOf(endMarker);

        if (startIndex == -1 || endIndex == -1)
            throw new FormatException($"Invalid PEM format: missing markers for {keyType}");

        startIndex += beginMarker.Length;
        var base64 = pemData.Substring(startIndex, endIndex - startIndex);

        // Remove whitespace
        return base64.Replace("\r", "").Replace("\n", "").Replace(" ", "");
    }

    /// <summary>
    /// Read 32-bit integer in big-endian format (network byte order)
    /// </summary>
    private static int ReadInt32BigEndian(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(4);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToInt32(bytes, 0);
    }
}

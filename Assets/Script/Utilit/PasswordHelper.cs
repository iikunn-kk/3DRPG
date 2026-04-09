using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// 密码处理辅助类，用于创建和验证加盐哈希密码。
/// </summary>
public static class PasswordHelper
{
    /// <summary>
    /// 为给定的密码创建一个哈希值和盐。
    /// </summary>
    /// <param name="password">用户输入的原始密码。</param>
    /// <param name="passwordHash">输出的哈希值 (Base64字符串)。</param>
    /// <param name="passwordSalt">输出的盐 (Base64字符串)。</param>
    public static void CreatePasswordHash(string password, out string passwordHash, out string passwordSalt)
    {
        if (string.IsNullOrEmpty(password)) throw new ArgumentNullException(nameof(password));

        // 使用HMACSHA512算法，它会自动生成一个随机密钥（这个密钥就是我们的“盐”）
        using (var hmac = new HMACSHA512())
        {
            passwordSalt = Convert.ToBase64String(hmac.Key);
            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
            passwordHash = Convert.ToBase64String(computedHash);
        }
    }

    /// <summary>
    /// 验证给定密码是否与存储的哈希和盐匹配。
    /// </summary>
    /// <param name="password">用户本次输入的密码。</param>
    /// <param name="storedHash">数据库中存储的哈希值。</param>
    /// <param name="storedSalt">数据库中存储的盐。</param>
    /// <returns>如果密码匹配则返回true，否则返回false。</returns>
    public static bool VerifyPasswordHash(string password, string storedHash, string storedSalt)
    {
        if (string.IsNullOrEmpty(password)) throw new ArgumentNullException(nameof(password));
        if (string.IsNullOrEmpty(storedHash)) throw new ArgumentNullException(nameof(storedHash));
        if (string.IsNullOrEmpty(storedSalt)) throw new ArgumentNullException(nameof(storedSalt));

        var saltBytes = Convert.FromBase64String(storedSalt);
        
        // 使用存储的盐重新创建HMACSHA512实例
        using (var hmac = new HMACSHA512(saltBytes))
        {
            // 计算输入密码的哈希值
            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
            var storedHashBytes = Convert.FromBase64String(storedHash);

            // 比较两个哈希值是否完全相同
            return computedHash.SequenceEqual(storedHashBytes);
        }
    }
}
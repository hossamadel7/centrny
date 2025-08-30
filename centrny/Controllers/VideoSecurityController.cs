using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using centrny.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using centrny.Attributes;

namespace centrny.Controllers
{
    // Comment this out temporarily if you haven't set up the VideoSecurity page in database yet
    // [RequirePageAccess("VideoSecurity")]
    public class VideoSecurityController : Controller
    {
        private readonly CenterContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<VideoSecurityController> _logger;

        // Security constants - in production, move these to appsettings.json
        private readonly int TOKEN_EXPIRY_MINUTES = 30;
        private readonly int MAX_CONCURRENT_SESSIONS = 2;
        private readonly string JWT_SECRET_KEY = "your-very-long-secret-key-for-jwt-token-signing-at-least-256-bits-long!";
        private readonly string ENCRYPTION_KEY = "12345678901234567890123456789012"; // Exactly 32 bytes

        public VideoSecurityController(CenterContext context, IConfiguration configuration, ILogger<VideoSecurityController> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Main view to test video security
        /// </summary>
        public IActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// Generate a secure video access link for testing - NO DATABASE REQUIRED
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> GenerateSecureLink([FromBody] VideoLinkRequest request)
        {
            try
            {
                // Extract file ID from Google Drive URL if needed
                var videoId = ExtractDriveFileId(request.VideoId);

                // MOCK DATA - No database calls
                var mockStudentId = 1;
                var mockStudentName = "Test Student";
                var mockBranchName = "Test Branch";

                // Generate device fingerprint
                var deviceFingerprint = GenerateDeviceFingerprint();

                // Create JWT token with video access claims
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(JWT_SECRET_KEY);
                var expiryTime = DateTime.UtcNow.AddMinutes(TOKEN_EXPIRY_MINUTES);

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(new[]
                    {
                        new Claim("userId", "1"),
                        new Claim("studentId", mockStudentId.ToString()),
                        new Claim("videoId", videoId),
                        new Claim("lessonId", request.LessonId?.ToString() ?? "0"),
                        new Claim("deviceFingerprint", deviceFingerprint),
                        new Claim("sessionId", Guid.NewGuid().ToString()),
                        new Claim("watermark", mockStudentName),
                        new Claim("branchCode", "1")
                    }),
                    Expires = expiryTime,
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key),
                        SecurityAlgorithms.HmacSha256Signature)
                };

                var token = tokenHandler.CreateToken(tokenDescriptor);
                var tokenString = tokenHandler.WriteToken(token);

                // Generate secure proxy URL - no additional URL encoding needed with URL-safe Base64
                var encryptedToken = EncryptString(tokenString);
                var proxyUrl = Url.Action("VideoProxy", "VideoSecurity",
                    new { token = encryptedToken, videoId = videoId },
                    Request.Scheme);

                // Determine video type and original URL
                string originalUrl;
                string videoType;

                if (videoId.Length > 20) // Google Drive
                {
                    originalUrl = $"https://drive.google.com/file/d/{videoId}/view";
                    videoType = "Google Drive";
                }
                else // YouTube
                {
                    originalUrl = $"https://www.youtube.com/watch?v={videoId}";
                    videoType = "YouTube";
                }

                _logger.LogInformation($"Secure video link generated for testing - {videoType} video {videoId}");

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        originalVideoId = videoId,
                        videoType = videoType,
                        originalUrl = originalUrl,
                        youtubeUrl = videoType == "YouTube" ? originalUrl : null,
                        driveUrl = videoType == "Google Drive" ? originalUrl : null,
                        secureProxyUrl = proxyUrl,
                        token = tokenString.Substring(0, 50) + "...", // Show partial token for testing
                        expiresAt = expiryTime,
                        expiresIn = $"{TOKEN_EXPIRY_MINUTES} minutes",
                        studentName = mockStudentName,
                        branchName = mockBranchName,
                        watermark = mockStudentName,
                        sessionInfo = "Use the secureProxyUrl in your iframe instead of direct video URL",
                        hackingNotes = new
                        {
                            tokenStructure = "JWT token encrypted with AES-256",
                            deviceBinding = "Bound to browser fingerprint",
                            timeLimit = $"Expires in {TOKEN_EXPIRY_MINUTES} minutes",
                            urlStructure = "Encrypted token + video ID validation",
                            videoProtection = videoType == "Google Drive" ?
                                "Google Drive sharing permissions still apply" :
                                "YouTube embed with restricted parameters",
                            securityLayers = new[] {
                                "Device fingerprinting",
                                "JWT token validation",
                                "AES encryption",
                                "Time-based expiry",
                                "Video ID binding",
                                videoType == "Google Drive" ? "Drive access control" : "YouTube embed restrictions"
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating secure video link");
                return Json(new { success = false, message = $"Internal server error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Video proxy endpoint - serves the actual video content through secure channel - NO DATABASE
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> VideoProxy(string token, string videoId)
        {
            try
            {
                // Decrypt and validate token
                var decryptedToken = DecryptString(token);
                var tokenHandler = new JwtSecurityTokenHandler();

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(JWT_SECRET_KEY)),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(decryptedToken, validationParameters, out _);

                // Extract claims
                var studentIdClaim = principal.FindFirst("studentId")?.Value;
                var videoIdClaim = principal.FindFirst("videoId")?.Value;
                var deviceFingerprintClaim = principal.FindFirst("deviceFingerprint")?.Value;
                var watermarkClaim = principal.FindFirst("watermark")?.Value;

                // Validate video ID matches
                if (videoIdClaim != videoId)
                {
                    return Unauthorized("Token-video mismatch - This is a security violation!");
                }

                // Verify device fingerprint (basic security check)
                var currentDeviceFingerprint = GenerateDeviceFingerprint();
                if (deviceFingerprintClaim != currentDeviceFingerprint)
                {
                    _logger.LogWarning($"Device fingerprint mismatch for student {studentIdClaim}");
                    return Unauthorized("Device fingerprint mismatch - Possible account sharing detected!");
                }

                // Generate the secure video player HTML with watermark
                var videoPlayerHtml = GenerateSecureVideoPlayer(videoId, watermarkClaim ?? "Test Student");

                // Return the video player with security headers
                Response.Headers.Add("X-Frame-Options", "SAMEORIGIN");
                Response.Headers.Add("X-Content-Type-Options", "nosniff");
                Response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
                Response.Headers.Add("Pragma", "no-cache");
                Response.Headers.Add("Expires", "0");

                return Content(videoPlayerHtml, "text/html");
            }
            catch (SecurityTokenExpiredException)
            {
                return Unauthorized("Session expired - Token has timed out!");
            }
            catch (SecurityTokenValidationException ex)
            {
                return Unauthorized($"Invalid token - {ex.Message}");
            }
            catch (FormatException)
            {
                return Unauthorized("Invalid token format - Token has been tampered with!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in video proxy for video {VideoId}", videoId);
                return StatusCode(500, $"Unable to load video content: {ex.Message}");
            }
        }

        /// <summary>
        /// Quick endpoint to test JWT token generation without database dependencies
        /// </summary>
        [HttpGet]
        public IActionResult TestToken(string videoId = "dQw4w9WgXcQ")
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(JWT_SECRET_KEY);
                var expiryTime = DateTime.UtcNow.AddMinutes(TOKEN_EXPIRY_MINUTES);

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(new[]
                    {
                        new Claim("userId", "test-user"),
                        new Claim("studentId", "1"),
                        new Claim("videoId", videoId),
                        new Claim("deviceFingerprint", "test-device"),
                        new Claim("sessionId", Guid.NewGuid().ToString()),
                        new Claim("watermark", "Test Student")
                    }),
                    Expires = expiryTime,
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key),
                        SecurityAlgorithms.HmacSha256Signature)
                };

                var token = tokenHandler.CreateToken(tokenDescriptor);
                var tokenString = tokenHandler.WriteToken(token);

                // Test encryption/decryption
                var encrypted = EncryptString(tokenString);
                var decrypted = DecryptString(encrypted);
                var encryptionTest = decrypted == tokenString;

                return Json(new
                {
                    success = true,
                    tokenGenerated = true,
                    tokenLength = tokenString.Length,
                    tokenPreview = tokenString.Substring(0, Math.Min(50, tokenString.Length)) + "...",
                    expiresAt = expiryTime,
                    encryptionTest = encryptionTest ? "✅ Passed" : "❌ Failed",
                    securityFeatures = new
                    {
                        jwtSigning = "✅ HMAC-SHA256",
                        aesEncryption = "✅ AES-256",
                        deviceFingerprinting = "✅ Enabled",
                        tokenExpiry = $"✅ {TOKEN_EXPIRY_MINUTES} minutes"
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Test direct link generation without AJAX
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> QuickTest(string videoId = "dQw4w9WgXcQ")
        {
            var request = new VideoLinkRequest { VideoId = videoId };
            var result = await GenerateSecureLink(request);

            if (result is JsonResult jsonResult)
            {
                ViewBag.Model = jsonResult.Value;
                return View();
            }

            ViewBag.Model = new { success = false, message = "Failed to generate link" };
            return View();
        }

        /// <summary>
        /// Get test video link - simple GET endpoint for quick testing
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetTestLink(string videoId = "dQw4w9WgXcQ", int? lessonId = null)
        {
            var request = new VideoLinkRequest
            {
                VideoId = videoId,
                LessonId = lessonId
            };

            // Call the existing GenerateSecureLink method
            var result = await GenerateSecureLink(request);
            return result;
        }

        #region Helper Methods

        private string GenerateDeviceFingerprint()
        {
            var userAgent = Request.Headers["User-Agent"].ToString();
            var acceptLanguage = Request.Headers["Accept-Language"].ToString();
            var ipAddress = GetClientIpAddress();

            var fingerprint = $"{userAgent}{acceptLanguage}{ipAddress}";

            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(fingerprint));
                return Convert.ToBase64String(hash);
            }
        }

        private string GetClientIpAddress()
        {
            var ipAddress = Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (string.IsNullOrEmpty(ipAddress))
                ipAddress = Request.Headers["X-Real-IP"].FirstOrDefault();
            if (string.IsNullOrEmpty(ipAddress))
                ipAddress = Request.HttpContext.Connection.RemoteIpAddress?.ToString();

            return ipAddress ?? "unknown";
        }

        private string EncryptString(string plainText)
        {
            byte[] iv = new byte[16];
            byte[] array;

            using (Aes aes = Aes.Create())
            {
                // Ensure key is exactly 32 bytes
                var keyBytes = Encoding.UTF8.GetBytes(ENCRYPTION_KEY);
                if (keyBytes.Length != 32)
                {
                    // Pad or truncate to exactly 32 bytes
                    Array.Resize(ref keyBytes, 32);
                }

                aes.Key = keyBytes;
                aes.IV = iv;

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter streamWriter = new StreamWriter(cryptoStream))
                        {
                            streamWriter.Write(plainText);
                        }
                        array = memoryStream.ToArray();
                    }
                }
            }

            // Convert to URL-safe Base64
            return Convert.ToBase64String(array)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        private string DecryptString(string cipherText)
        {
            // Convert from URL-safe Base64
            var base64 = cipherText
                .Replace('-', '+')
                .Replace('_', '/');

            // Add padding if needed
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }

            byte[] iv = new byte[16];
            byte[] buffer = Convert.FromBase64String(base64);

            using (Aes aes = Aes.Create())
            {
                // Ensure key is exactly 32 bytes
                var keyBytes = Encoding.UTF8.GetBytes(ENCRYPTION_KEY);
                if (keyBytes.Length != 32)
                {
                    // Pad or truncate to exactly 32 bytes
                    Array.Resize(ref keyBytes, 32);
                }

                aes.Key = keyBytes;
                aes.IV = iv;
                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                using (MemoryStream memoryStream = new MemoryStream(buffer))
                {
                    using (CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader streamReader = new StreamReader(cryptoStream))
                        {
                            return streamReader.ReadToEnd();
                        }
                    }
                }
            }
        }

        private string GenerateSecureVideoPlayer(string videoId, string watermark)
        {
            // Determine if this is a Google Drive file ID or YouTube video ID
            string iframeSrc;
            string videoType;

            if (videoId.Length > 20) // Google Drive file IDs are typically 33+ characters
            {
                iframeSrc = $"https://drive.google.com/file/d/{videoId}/preview";
                videoType = "Google Drive";
            }
            else
            {
                iframeSrc = $"https://www.youtube.com/embed/{videoId}?autoplay=1&controls=1&showinfo=0&rel=0&modestbranding=1";
                videoType = "YouTube";
            }

            return $@"
<!DOCTYPE html>
<html>
<head>
    <title>Secure Video Player</title>
    <style>
        body {{ margin: 0; padding: 0; background: #000; font-family: Arial, sans-serif; }}
        .video-container {{ position: relative; width: 100%; height: 100vh; }}
        .watermark {{ 
            position: absolute; 
            top: 10px; 
            right: 10px; 
            color: rgba(255,255,255,0.7); 
            background: rgba(0,0,0,0.5);
            padding: 5px 10px;
            border-radius: 5px;
            font-size: 14px;
            z-index: 1000;
        }}
        iframe {{ 
            width: 100%; 
            height: 100%; 
            border: none; 
        }}
        .info {{ 
            position: absolute; 
            bottom: 10px; 
            left: 10px; 
            color: rgba(255,255,255,0.7); 
            background: rgba(0,0,0,0.7);
            padding: 10px;
            border-radius: 5px;
            font-size: 12px;
            max-width: 300px;
        }}
        .security-banner {{
            position: absolute;
            top: 0;
            left: 0;
            right: 0;
            background: rgba(220, 53, 69, 0.9);
            color: white;
            text-align: center;
            padding: 5px;
            font-size: 12px;
            z-index: 999;
        }}
    </style>
</head>
<body>
    <div class='video-container'>
        <div class='security-banner'>SECURE ACCESS - UNAUTHORIZED SHARING PROHIBITED</div>
        <div class='watermark'>{watermark}</div>
        <iframe 
            src='{iframeSrc}' 
            allow='accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture' 
            allowfullscreen>
        </iframe>
        <div class='info'>
            <strong>Secure Video Access</strong><br>
            Video Type: {videoType}<br>
            Video ID: {videoId}<br>
            Student: {watermark}<br>
            Session expires in 30 minutes
        </div>
    </div>
    
    <script>
        // Prevent right-click context menu
        document.addEventListener('contextmenu', function(e) {{
            e.preventDefault();
        }});
        
        // Disable F12 and other developer tools shortcuts
        document.addEventListener('keydown', function(e) {{
            if (e.key === 'F12' || 
                (e.ctrlKey && e.shiftKey && e.key === 'I') ||
                (e.ctrlKey && e.shiftKey && e.key === 'C') ||
                (e.ctrlKey && e.key === 'u')) {{
                e.preventDefault();
            }}
        }});
        
        // Log access attempt
        console.log('Secure video session started at: ' + new Date().toISOString());
        console.log('Video type: {videoType}');
        console.log('Video ID: {videoId}');
    </script>
</body>
</html>";
        }

        /// <summary>
        /// Extract file ID from Google Drive URL
        /// </summary>
        private string ExtractDriveFileId(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Handle full Google Drive URLs
            if (input.Contains("drive.google.com"))
            {
                var patterns = new[]
                {
                    @"https://drive\.google\.com/file/d/([a-zA-Z0-9_-]+)",
                    @"https://drive\.google\.com/open\?id=([a-zA-Z0-9_-]+)"
                };

                foreach (var pattern in patterns)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(input, pattern);
                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }
                }
            }

            // If it's already just a file ID or YouTube video ID, return as is
            return input;
        }

        #endregion
    }

    #region Request Models
    public class VideoLinkRequest
    {
        public string VideoId { get; set; } = string.Empty;
        public int? LessonId { get; set; }
    }
    #endregion
}
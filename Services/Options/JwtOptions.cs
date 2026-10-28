using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Services.Options
{
    public sealed class JwtOptions
    {
        public const string SectionName = "JWT";

        private string _secretKey = null!;
        private SymmetricSecurityKey? _signingKey;

        public string SecretKey
        {
            get => _secretKey;
            set
            {
                _secretKey = value;
                _signingKey = null;
            }
        }

        public string Issuer { get; set; } = null!;
        public string Audience { get; set; } = null!;
        public int AccessTokenExpirationMinutes { get; set; } = 15;
        public int RefreshTokenExpirationDays { get; set; } = 7;

        public bool IsValid()
            => !string.IsNullOrWhiteSpace(Issuer)
                && !string.IsNullOrWhiteSpace(Audience)
                && !string.IsNullOrWhiteSpace(SecretKey)
                && Encoding.UTF8.GetByteCount(SecretKey) >= 32
                && AccessTokenExpirationMinutes > 0
                && RefreshTokenExpirationDays > 0;

        public SymmetricSecurityKey GetSigningKey()
        {
            if (!IsValid())
            {
                throw new InvalidOperationException(JwtOptionsValidation.ErrorMessage);
            }

            return _signingKey ??= new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey));
        }
    }
}

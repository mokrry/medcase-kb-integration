using MedicalFeaturePrototype.Api.Data;
using MedicalFeaturePrototype.Api.Entities;
using MedicalFeaturePrototype.Api.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MedicalFeaturePrototype.Api.Services;

public class AdminBootstrapService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<AdminBootstrapService> _logger;
    private readonly IOptions<SeedAdminOptions> _options;
    private readonly PasswordHasher<ApplicationUser> _passwordHasher = new();

    public AdminBootstrapService(
        ApplicationDbContext dbContext,
        IOptions<SeedAdminOptions> options,
        ILogger<AdminBootstrapService> logger)
    {
        _dbContext = dbContext;
        _options = options;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        if (string.IsNullOrWhiteSpace(options.Email) || string.IsNullOrWhiteSpace(options.Password))
        {
            _logger.LogInformation("[SERVER] Seed admin is not configured");
            return;
        }

        if (options.Password.Length < 8)
        {
            _logger.LogWarning("[SERVER] Seed admin password is too short; admin was not created");
            return;
        }

        var normalizedEmail = options.Email.Trim().ToUpperInvariant();
        var existingUser = await _dbContext.Users
            .FirstOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);

        if (existingUser is not null)
        {
            if (existingUser.Role != UserRoles.Admin)
            {
                existingUser.Role = UserRoles.Admin;
                existingUser.IsActive = true;
                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("[SERVER] Existing user promoted to admin Email={Email}", existingUser.Email);
            }

            return;
        }

        var user = new ApplicationUser
        {
            Email = options.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            Role = UserRoles.Admin,
            IsActive = true
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, options.Password);

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("[SERVER] Seed admin created Email={Email}", user.Email);
    }
}

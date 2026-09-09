using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sgf.Application.Identity;
using Sgf.Domain.Companies;
using Sgf.Infrastructure.Database;

namespace Sgf.Infrastructure.Identity.Registration;

public sealed class RegisterCompanyOwnerService : IRegisterCompanyOwnerUseCase
{
    private const int MaxUserNameLength = 200;

    private readonly SgfDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public RegisterCompanyOwnerService(
        SgfDbContext dbContext,
        UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
    }

    public async Task<RegistrationResult> ExecuteAsync(
        RegisterCompanyOwnerRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = ValidateRequest(request);

        if (validationErrors.Count > 0)
        {
            return ValidationFailure(validationErrors);
        }

        var normalizedEmail = request.Email.Trim();
        var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);

        if (existingUser is not null)
        {
            return RegistrationResult.Failure(new RegistrationError(
                RegistrationErrorCode.EmailAlreadyRegistered,
                "E-mail already registered.",
                ["Use another e-mail address."]));
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var user = new ApplicationUser
            {
                Name = request.Name.Trim(),
                Email = normalizedEmail,
                UserName = normalizedEmail
            };

            var identityResult = await _userManager.CreateAsync(user, request.Password);

            if (!identityResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);

                return ValidationFailure(identityResult.Errors.Select(error => error.Description));
            }

            var company = new Company(request.CompanyName);
            var membership = new Membership(user.Id, company.Id, MembershipRole.Owner);

            _dbContext.Companies.Add(company);
            _dbContext.Memberships.Add(membership);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return RegistrationResult.Success(new RegisterCompanyOwnerResponse(
                user.Id,
                company.Id,
                membership.Id,
                membership.Role.ToString()));
        }
        catch (Exception) when (_dbContext.Database.CurrentTransaction is not null)
        {
            await transaction.RollbackAsync(cancellationToken);

            return RegistrationResult.Failure(new RegistrationError(
                RegistrationErrorCode.PersistenceFailed,
                "Registration could not be completed.",
                ["No partial registration was kept."]));
        }
    }

    private static List<string> ValidateRequest(RegisterCompanyOwnerRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors.Add("Name is required.");
        }
        else if (request.Name.Trim().Length > MaxUserNameLength)
        {
            errors.Add($"Name must be at most {MaxUserNameLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors.Add("E-mail is required.");
        }
        else if (!new EmailAddressAttribute().IsValid(request.Email.Trim()))
        {
            errors.Add("E-mail format is invalid.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            errors.Add("Password is required.");
        }

        if (string.IsNullOrWhiteSpace(request.CompanyName))
        {
            errors.Add("Company name is required.");
        }

        return errors;
    }

    private static RegistrationResult ValidationFailure(IEnumerable<string> details)
    {
        return RegistrationResult.Failure(new RegistrationError(
            RegistrationErrorCode.ValidationFailed,
            "Registration data is invalid.",
            details.ToArray()));
    }
}

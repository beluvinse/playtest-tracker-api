using Microsoft.AspNetCore.Identity;
using PlaytestTracker.Api.Data;
using PlaytestTracker.Api.Models;

namespace PlaytestTracker.Api.Services
{
    // The rules for creating an account and checking a login. It doesn't know about HTTP or tokens:
    // the controller turns its answers into responses, and a token service (next branch) issues
    // the token after a successful login.
    public class AuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AppDbContext _context;
        private readonly TimeProvider _clock;

        public AuthService(UserManager<ApplicationUser> userManager, AppDbContext context, TimeProvider clock)
        {
            _userManager = userManager;
            _context = context;
            _clock = clock;
        }

        public async Task<RegisterResult> RegisterAsync(string email, string password, string displayName)
        {
            var user = new ApplicationUser
            {
                // The email is also the username: Identity's database index only guarantees a
                // unique username, so this makes the database back up "one account per email"
                UserName = email,
                Email = email,
                DisplayName = displayName
            };

            // The account and its personal organization are saved together or not at all: if the
            // second step failed after the first, there would be a person with nowhere to work.
            // Everything inside the transaction is only made permanent by Commit.
            await using var transaction = await _context.Database.BeginTransactionAsync();

            // CreateAsync checks the email and the password rules, hashes the password with a random
            // salt (the same password gives a different hash for each person) and saves the user.
            // The password itself is never stored.
            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                await CreatePersonalOrganizationAsync(user);
                await transaction.CommitAsync();
                return new RegisterResult { Status = RegisterStatus.Success, User = user };
            }

            var errors = result.Errors.ToList();

            if (errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
                return new RegisterResult { Status = RegisterStatus.EmailTaken };

            if (errors.Any(e => e.Code is "InvalidEmail" or "InvalidUserName"))
                return new RegisterResult { Status = RegisterStatus.InvalidEmail };

            return new RegisterResult
            {
                Status = RegisterStatus.InvalidPassword,
                PasswordErrors = errors.Select(e => e.Description).ToList()
            };
        }

        public async Task<LoginResult> CheckCredentialsAsync(string email, string password)
        {
            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
                return new LoginResult { Status = LoginStatus.InvalidCredentials };

            // A paused account refuses even the right password, or the pause would be pointless
            if (await _userManager.IsLockedOutAsync(user))
                return LockedOut(user);

            if (!await _userManager.CheckPasswordAsync(user, password))
            {
                // Counts the failure, and pauses the account once the limit is reached
                await _userManager.AccessFailedAsync(user);

                return await _userManager.IsLockedOutAsync(user)
                    ? LockedOut(user)
                    : new LoginResult { Status = LoginStatus.InvalidCredentials };
            }

            // A good login forgets the earlier failed attempts, so they don't pile up over weeks
            await _userManager.ResetAccessFailedCountAsync(user);

            return new LoginResult { Status = LoginStatus.Success, User = user };
        }

        // For "who am I": the token says which id it belongs to, and this checks that person still exists
        public async Task<ApplicationUser?> FindByIdAsync(string id)
        {
            return await _userManager.FindByIdAsync(id);
        }

        // Every new account starts with a space of its own, where the person is the Owner. This way
        // nobody is ever without an organization, and the app always has somewhere to put their projects.
        private async Task CreatePersonalOrganizationAsync(ApplicationUser user)
        {
            var now = _clock.GetUtcNow();

            _context.Organizations.Add(new Organization
            {
                Name = $"{user.DisplayName}'s space",
                CreatedAt = now,
                Members =
                {
                    new Membership { UserId = user.Id, Role = OrganizationRole.Owner, JoinedAt = now }
                }
            });

            await _context.SaveChangesAsync();
        }

        private static LoginResult LockedOut(ApplicationUser user)
        {
            return new LoginResult { Status = LoginStatus.LockedOut, LockedUntil = user.LockoutEnd };
        }
    }
}

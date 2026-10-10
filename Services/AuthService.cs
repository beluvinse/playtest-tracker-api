using Microsoft.AspNetCore.Identity;
using PlaytestTracker.Api.Models;

namespace PlaytestTracker.Api.Services
{
    // The rules for creating an account and checking a login. It doesn't know about HTTP or tokens:
    // the controller turns its answers into responses, and a token service (next branch) issues
    // the token after a successful login.
    public class AuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public AuthService(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
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

            // CreateAsync checks the email and the password rules, hashes the password with a random
            // salt (the same password gives a different hash for each person) and saves the user.
            // The password itself is never stored.
            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
                return new RegisterResult { Status = RegisterStatus.Success, User = user };

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

        private static LoginResult LockedOut(ApplicationUser user)
        {
            return new LoginResult { Status = LoginStatus.LockedOut, LockedUntil = user.LockoutEnd };
        }
    }
}

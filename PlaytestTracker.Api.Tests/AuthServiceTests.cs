using Microsoft.EntityFrameworkCore;
using PlaytestTracker.Api.Data;
using PlaytestTracker.Api.Models;
using PlaytestTracker.Api.Services;
using PlaytestTracker.Api.Tests.Support;

namespace PlaytestTracker.Api.Tests;

// xUnit creates a new instance of this class for every test, so every test starts with
// a new TestApp: an empty database and no accounts
public class AuthServiceTests : IDisposable
{
    // Test values only: they meet the password rules (8+ characters, a lowercase letter, a digit)
    private const string Email = "ana@test.local";
    private const string Password = "Sunflower7";
    private const string WrongPassword = "Wrongpass1";

    private readonly TestApp _app = new();
    private readonly AuthService _auth;
    private readonly AppDbContext _db;

    public AuthServiceTests()
    {
        _auth = _app.Get<AuthService>();
        _db = _app.Get<AppDbContext>();
    }

    public void Dispose() => _app.Dispose();

    private Task<RegisterResult> Register(string email = Email, string password = Password, string name = "Ana")
        => _auth.RegisterAsync(email, password, name);

    private Task<ApplicationUser> StoredUser(string email = Email)
        => _db.Users.SingleAsync(user => user.Email == email);

    // ---- Registration ----

    [Fact]
    public async Task Register_WithValidData_CreatesTheAccount()
    {
        var result = await Register(name: "Ana");

        Assert.Equal(RegisterStatus.Success, result.Status);

        var user = await StoredUser();
        Assert.Equal(Email, user.UserName); // the email is also the username
        Assert.Equal("Ana", user.DisplayName);
    }

    [Fact]
    public async Task Register_NeverStoresThePasswordItself()
    {
        await Register();

        var user = await StoredUser();
        Assert.False(string.IsNullOrEmpty(user.PasswordHash));
        Assert.NotEqual(Password, user.PasswordHash);
        Assert.DoesNotContain(Password, user.PasswordHash);
    }

    [Fact]
    public async Task Register_TwoPeopleWithTheSamePassword_GetDifferentHashes()
    {
        // Each hash includes a random salt, so a leaked database doesn't show who shares a password
        await Register("ana@test.local");
        await Register("luis@test.local");

        var ana = await StoredUser("ana@test.local");
        var luis = await StoredUser("luis@test.local");
        Assert.NotEqual(ana.PasswordHash, luis.PasswordHash);
    }

    [Theory]
    [InlineData("ana@test.local")]
    [InlineData("ANA@Test.Local")] // an email is the same email whatever its letter case
    public async Task Register_EmailAlreadyUsed_ReturnsEmailTaken(string secondEmail)
    {
        await Register("ana@test.local");

        var result = await Register(secondEmail);

        Assert.Equal(RegisterStatus.EmailTaken, result.Status);
        Assert.Equal(1, await _db.Users.CountAsync());
    }

    [Theory]
    [InlineData("abcdef1", "at least 8 characters")]
    [InlineData("sunflowerabc", "digit")]
    [InlineData("SUNFLOWER77", "lowercase")]
    public async Task Register_WeakPassword_ReturnsInvalidPasswordAndCreatesNothing(
        string weakPassword, string expectedProblem)
    {
        var result = await Register(password: weakPassword);

        Assert.Equal(RegisterStatus.InvalidPassword, result.Status);
        Assert.Contains(result.PasswordErrors, error => error.Contains(expectedProblem));
        Assert.Equal(0, await _db.Users.CountAsync());
    }

    [Fact]
    public async Task Register_ThingThatIsNotAnEmail_ReturnsInvalidEmail()
    {
        var result = await Register(email: "not-an-email");

        Assert.Equal(RegisterStatus.InvalidEmail, result.Status);
        Assert.Equal(0, await _db.Users.CountAsync());
    }

    // ---- Login ----

    [Theory]
    [InlineData("ana@test.local")]
    [InlineData("ANA@TEST.LOCAL")]
    public async Task Login_WithTheCorrectPassword_Succeeds(string loginEmail)
    {
        await Register("ana@test.local");

        var result = await _auth.CheckCredentialsAsync(loginEmail, Password);

        Assert.Equal(LoginStatus.Success, result.Status);
        Assert.Equal("ana@test.local", result.User!.Email);
    }

    [Fact]
    public async Task Login_WithAWrongPassword_ReturnsInvalidCredentials()
    {
        await Register();

        var result = await _auth.CheckCredentialsAsync(Email, WrongPassword);

        Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
        Assert.Null(result.User);
    }

    [Fact]
    public async Task Login_UnknownEmail_GivesTheSameAnswerAsAWrongPassword()
    {
        // If these differed, anyone could use the login form to find out which emails have an account
        await Register();

        var wrongPassword = await _auth.CheckCredentialsAsync(Email, WrongPassword);
        var unknownEmail = await _auth.CheckCredentialsAsync("ghost@test.local", WrongPassword);

        Assert.Equal(wrongPassword.Status, unknownEmail.Status);
    }

    [Fact]
    public async Task Login_FiveWrongPasswordsInARow_LocksTheAccount()
    {
        await Register();

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var result = await _auth.CheckCredentialsAsync(Email, WrongPassword);
            Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
        }

        var fifth = await _auth.CheckCredentialsAsync(Email, WrongPassword);

        Assert.Equal(LoginStatus.LockedOut, fifth.Status);
        Assert.True(fifth.LockedUntil > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Login_WhileLocked_RejectsEvenTheCorrectPassword()
    {
        await Register();
        for (var attempt = 1; attempt <= 5; attempt++)
            await _auth.CheckCredentialsAsync(Email, WrongPassword);

        var result = await _auth.CheckCredentialsAsync(Email, Password);

        Assert.Equal(LoginStatus.LockedOut, result.Status);
        Assert.Null(result.User);
    }

    [Fact]
    public async Task Login_AGoodLogin_ForgetsTheEarlierFailures()
    {
        await Register();
        for (var attempt = 1; attempt <= 4; attempt++)
            await _auth.CheckCredentialsAsync(Email, WrongPassword);

        // Four failures were not enough to lock it. A good login resets the count to zero…
        var good = await _auth.CheckCredentialsAsync(Email, Password);
        Assert.Equal(LoginStatus.Success, good.Status);

        // …so four more failures still don't lock it (without the reset, the first one would)
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var result = await _auth.CheckCredentialsAsync(Email, WrongPassword);
            Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
        }
    }

    [Fact]
    public async Task Login_OneLockedAccount_DoesNotAffectAnotherOne()
    {
        await Register("ana@test.local");
        await Register("luis@test.local");
        for (var attempt = 1; attempt <= 5; attempt++)
            await _auth.CheckCredentialsAsync("ana@test.local", WrongPassword);

        var result = await _auth.CheckCredentialsAsync("luis@test.local", Password);

        Assert.Equal(LoginStatus.Success, result.Status);
    }
}

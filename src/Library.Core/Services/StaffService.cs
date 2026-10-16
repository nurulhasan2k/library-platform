using Library.Core.Data;
using Library.Core.Models;

namespace Library.Core.Services;

/// <summary>Staff logins (admin, reception, manager).</summary>
public sealed class StaffService(ILibraryStore store)
{
    public User? Authenticate(string username, string password)
    {
        var user = store.Read(d => d.Users.FirstOrDefault(u => string.Equals(u.Username, username?.Trim(), StringComparison.OrdinalIgnoreCase)));
        var ok = PasswordHasher.Verify(password ?? "", user?.PasswordHash ?? PasswordHasher.Dummy);
        return ok && user != null ? user : null;
    }

    public User? GetUser(int id) => store.Read(d => d.Users.FirstOrDefault(u => u.Id == id));

    public IReadOnlyList<User> ListUsers() => store.Read(d => (IReadOnlyList<User>)d.Users.OrderBy(u => u.Role).ThenBy(u => u.Username).ToList());

    public User CreateUser(string username, string password, UserRole role, IEnumerable<int>? branchIds = null)
    {
        if (string.IsNullOrWhiteSpace(username)) throw new LibraryException("Username is required.");
        if (string.IsNullOrEmpty(password) || password.Length < 6) throw new LibraryException("Password must be at least 6 characters.");
        return store.Write(data =>
        {
            if (data.Users.Any(u => string.Equals(u.Username, username.Trim(), StringComparison.OrdinalIgnoreCase)))
                throw new LibraryException($"The username '{username.Trim()}' is already taken.");
            var ids = (branchIds ?? Array.Empty<int>()).Where(id => data.FindBranch(id) != null).Distinct().ToList();
            var u = new User { Id = data.TakeId(), Username = username.Trim(), PasswordHash = PasswordHasher.Hash(password), Role = role, BranchIds = ids };
            data.Users.Add(u);
            return u;
        });
    }

    public void SetBranches(int userId, IEnumerable<int> branchIds) => store.Write(data =>
    {
        var u = data.Users.FirstOrDefault(x => x.Id == userId) ?? throw new LibraryException("No such user.");
        u.BranchIds = branchIds.Where(id => data.FindBranch(id) != null).Distinct().ToList();
    });

    public void ResetPassword(int userId, string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 6) throw new LibraryException("Password must be at least 6 characters.");
        store.Write(data =>
        {
            var u = data.Users.FirstOrDefault(x => x.Id == userId) ?? throw new LibraryException("No such user.");
            u.PasswordHash = PasswordHasher.Hash(password);
        });
    }

    public void DeleteUser(int userId, int actingUserId)
    {
        store.Write(data =>
        {
            var u = data.Users.FirstOrDefault(x => x.Id == userId) ?? throw new LibraryException("No such user.");
            if (u.Id == actingUserId) throw new LibraryException("You can't delete the account you are signed in with.");
            if (u.Role == UserRole.Admin && data.Users.Count(x => x.Role == UserRole.Admin) == 1)
                throw new LibraryException("The last administrator can't be deleted.");
            data.Users.Remove(u);
        });
    }
}

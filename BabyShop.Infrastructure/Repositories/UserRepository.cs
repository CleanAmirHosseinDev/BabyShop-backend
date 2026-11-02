using BabyShop.Core.Entities;
using BabyShop.Core.Interfaces;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.Linq.Expressions;

namespace BabyShop.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly string _connectionString;

    public UserRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection");
    }

    // ============ متدهای IUserRepository ============

    public async Task<User?> GetByPhoneNumberAsync(string phoneNumber)
    {
        using var connection = new SqlConnection(_connectionString);

        return await connection.QueryFirstOrDefaultAsync<User>(
            "GetUserByPhoneNumber",
            new { PhoneNumber = phoneNumber },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        using var connection = new SqlConnection(_connectionString);

        return await connection.QueryFirstOrDefaultAsync<User>(
            "GetUserByEmail",
            new { Email = email },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task<bool> PhoneExistsAsync(string phoneNumber)
    {
        using var connection = new SqlConnection(_connectionString);

        var result = await connection.QueryFirstOrDefaultAsync<int>(
            "CheckPhoneExists",
            new { PhoneNumber = phoneNumber },
            commandType: CommandType.StoredProcedure
        );

        return result == 1;
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        using var connection = new SqlConnection(_connectionString);

        var result = await connection.QueryFirstOrDefaultAsync<int>(
            "CheckEmailExists",
            new { Email = email },
            commandType: CommandType.StoredProcedure
        );

        return result == 1;
    }

    public async Task<bool> CheckPhoneExistsAsync(string phoneNumber)
    {
        return await PhoneExistsAsync(phoneNumber);
    }

    public async Task<User> CreateUserAsync(User user)
    {
        using var connection = new SqlConnection(_connectionString);

        var parameters = new DynamicParameters();
        parameters.Add("@PhoneNumber", user.PhoneNumber);
        parameters.Add("@Email", user.Email);
        parameters.Add("@FullName", user.FullName);
        parameters.Add("@PasswordHash", user.PasswordHash);
        parameters.Add("@Id", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(
            "InsertUser",
            parameters,
            commandType: CommandType.StoredProcedure
        );

        user.Id = parameters.Get<int>("@Id");
        return user;
    }

    public async Task UpdateVerificationCodeAsync(int userId, string code)
    {
        using var connection = new SqlConnection(_connectionString);

        // کد تأیید هرگز نباید لاگ شود؛ فقط آن را در دیتابیس می‌نویسیم.
        await connection.ExecuteAsync(
            "UpdateVerificationCode",
            new { UserId = userId, Code = code },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task VerifyPhoneAsync(int userId)
    {
        using var connection = new SqlConnection(_connectionString);

        await connection.ExecuteAsync(
            "VerifyPhone",
            new { UserId = userId },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task UpdateLastLoginAsync(int userId)
    {
        using var connection = new SqlConnection(_connectionString);

        await connection.ExecuteAsync(
            "UpdateLastLogin",
            new { UserId = userId },
            commandType: CommandType.StoredProcedure
        );
    }
    public async Task UpdatePasswordAsync(int userId, string passwordHash)
    {
        using var connection = new SqlConnection(_connectionString);

        await connection.ExecuteAsync(
            @"UPDATE Users
              SET PasswordHash = @PasswordHash,
                  UpdatedAt = GETUTCDATE()
              WHERE Id = @UserId AND ISNULL(IsDeleted, 0) = 0",
            new { UserId = userId, PasswordHash = passwordHash }
        );
    }
    public async Task<IReadOnlyList<string>> GetUserRoleNamesAsync(int userId)
    {
        using var connection = new SqlConnection(_connectionString);

        var roles = await connection.QueryAsync<string>(
            @"SELECT r.Name
              FROM Roles r
              INNER JOIN UserRoles ur ON ur.RoleId = r.Id
              WHERE ur.UserId = @UserId
                AND ISNULL(ur.IsDeleted, 0) = 0
                AND ISNULL(r.IsDeleted, 0) = 0",
            new { UserId = userId }
        );

        return roles.AsList();
    }

    // ============ متدهای IRepository<User> ============

    public async Task<User> AddAsync(User entity)
    {
        return await CreateUserAsync(entity);
    }

    public async Task UpdateAsync(User entity)
    {
        using var connection = new SqlConnection(_connectionString);

        await connection.ExecuteAsync(
            "UpdateUser",
            new
            {
                Id = entity.Id,
                PhoneNumber = entity.PhoneNumber,
                Email = entity.Email,
                FullName = entity.FullName
            },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task<IReadOnlyList<User>> GetPagedAsync(int page, int pageSize)
    {
        using var connection = new SqlConnection(_connectionString);

        var result = await connection.QueryAsync<User>(
            "GetUsersPaged",
            new { Page = page, PageSize = pageSize },
            commandType: CommandType.StoredProcedure
        );

        return result.AsList();
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        using var connection = new SqlConnection(_connectionString);

        return await connection.QueryFirstOrDefaultAsync<User>(
            "GetUserById",
            new { Id = id },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task<IReadOnlyList<User>> GetAllAsync()
    {
        using var connection = new SqlConnection(_connectionString);

        var result = await connection.QueryAsync<User>(
            "GetAllUsers",
            commandType: CommandType.StoredProcedure
        );

        return result.AsList();
    }

    public async Task<User?> FirstOrDefaultAsync(Expression<Func<User, bool>> predicate)
    {
        var all = await GetAllAsync();
        return all.AsQueryable().FirstOrDefault(predicate.Compile());
    }

    public async Task<IReadOnlyList<User>> FindAsync(Expression<Func<User, bool>> predicate)
    {
        var all = await GetAllAsync();
        var result = all.AsQueryable().Where(predicate.Compile()).ToList();
        return result.AsReadOnly();
    }

    public async Task<bool> ExistsAsync(int id)
    {
        var user = await GetByIdAsync(id);
        return user != null;
    }

    public async Task DeleteAsync(User entity)
    {
        using var connection = new SqlConnection(_connectionString);

        await connection.ExecuteAsync(
            "DeleteUser",
            new { Id = entity.Id },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task<int> CountAsync()
    {
        using var connection = new SqlConnection(_connectionString);

        return await connection.ExecuteScalarAsync<int>(
            "GetUsersCount",
            commandType: CommandType.StoredProcedure
        );
    }
}
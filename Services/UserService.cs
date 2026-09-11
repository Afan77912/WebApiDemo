using Microsoft.AspNetCore.Identity;
using WebApiDemo.Models;
using WebApiDemo.Repositories;

namespace WebApiDemo.Services
{
    public class UserService
    {
        private readonly UserRepository _userRepository;
        private readonly PasswordHasher<User> _passwordHasher;

        public UserService(UserRepository userRepository)
        {
            _userRepository = userRepository;
            _passwordHasher = new PasswordHasher<User>();
        }

        // Register - Hash Password
        public async Task<string> HashPasswordAsync(
            User user,
            string password)
        {
            return _passwordHasher.HashPassword(
                user,
                password);
        }

        // Create User
        public async Task CreateUserAsync(User user)
        {
            await _userRepository.CreateAsync(user);
        }

        // Login - Verify Hashed Password
        public async Task<User?> ValidateUserAsync(
    string username,
    string password)
        {
            var user = await _userRepository
                .GetByUsernameAsync(username);

            if (user == null)
            {
                return null;
            }

            // Check if account is active
            if (!user.IsActive)
            {
                return null;
            }

            // Check if account is currently locked
            if (user.LockoutEnd != null &&
                user.LockoutEnd > DateTime.UtcNow)
            {
                return null;
            }

            var result = _passwordHasher.VerifyHashedPassword(
                user,
                user.Password,
                password);

            if (result == PasswordVerificationResult.Failed)
            {
                user.FailedLoginAttempts++;

                // Lock account after 5 failed attempts
                if (user.FailedLoginAttempts >= 5)
                {
                    user.LockoutEnd =
                        DateTime.UtcNow.AddMinutes(5);

                    user.FailedLoginAttempts = 0;
                }

                await _userRepository.UpdateAsync(user);

                return null;
            }


            if (result == PasswordVerificationResult.Failed)
            {
                user.FailedLoginAttempts++;

                Console.WriteLine(
                    $"Failed attempts: {user.FailedLoginAttempts}");

                if (user.FailedLoginAttempts >= 5)
                {
                    user.LockoutEnd =
                        DateTime.UtcNow.AddMinutes(5);

                    Console.WriteLine(
                        $"ACCOUNT LOCKED UNTIL: {user.LockoutEnd}");

                    user.FailedLoginAttempts = 0;
                }

                await _userRepository.UpdateAsync(user);

                return null;
            }
            // Successful login
            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;

            await _userRepository.UpdateAsync(user);

            return user;
        }

        // Update User
        public async Task UpdateUserAsync(User user)
        {
            await _userRepository.UpdateAsync(user);
        }

        // Find User by Refresh Token
        public async Task<User?> GetByRefreshTokenAsync(
            string refreshToken)
        {
            return await _userRepository
                .GetByRefreshTokenAsync(refreshToken);
        }

        public async Task<User?> GetByUsernameAsync(
    string username)
        {
            return await _userRepository
                .GetByUsernameAsync(username);
        }
    }
}
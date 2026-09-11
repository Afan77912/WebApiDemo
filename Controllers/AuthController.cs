using Microsoft.AspNetCore.Mvc;
using WebApiDemo.DTOs;
using WebApiDemo.Services;
using WebApiDemo.Models;

namespace WebApiDemo.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserService _userService;
        private readonly JwtService _jwtService;

        public AuthController(
            UserService userService,
            JwtService jwtService)
        {
            _userService = userService;
            _jwtService = jwtService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto loginDto)
        {
            var user = await _userService.ValidateUserAsync(
                loginDto.Username,
                loginDto.Password);

            if (user == null)
            {
                return Unauthorized(new
                {
                    Message = "Invalid username or password."
                });
            }

            // Generate Access Token
            var accessToken =
                _jwtService.GenerateToken(user);

            // Generate Refresh Token
            var refreshToken =
                _jwtService.GenerateRefreshToken();

            // Hash Refresh Token before storing in database
            var hashedRefreshToken =
                _jwtService.HashRefreshToken(refreshToken);

            user.RefreshToken = hashedRefreshToken;

            // Refresh Token valid for 7 days
            user.RefreshTokenExpiry =
                DateTime.UtcNow.AddDays(7);

            await _userService.UpdateUserAsync(user);

            return Ok(new
            {
                Message = "Login successful.",
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                Username = user.Username,
                Role = user.Role
            });
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken(
            RefreshTokenDto refreshTokenDto)
        {
            // Hash the incoming refresh token
            var hashedRefreshToken =
                _jwtService.HashRefreshToken(
                    refreshTokenDto.RefreshToken);

            // Find user using hashed refresh token
            var user = await _userService
                .GetByRefreshTokenAsync(
                    hashedRefreshToken);

            // Token not found
            if (user == null)
            {
                return Unauthorized(new
                {
                    Message = "Invalid refresh token."
                });
            }

            // Check token expiry
            if (user.RefreshTokenExpiry == null ||
                user.RefreshTokenExpiry <= DateTime.UtcNow)
            {
                return Unauthorized(new
                {
                    Message = "Refresh token has expired."
                });
            }

            // Generate new Access Token
            var accessToken =
                _jwtService.GenerateToken(user);

            // Generate new Refresh Token
            var newRefreshToken =
                _jwtService.GenerateRefreshToken();

            // Hash new Refresh Token before storing
            var hashedNewRefreshToken =
                _jwtService.HashRefreshToken(
                    newRefreshToken);

            // Replace old refresh token
            user.RefreshToken =
                hashedNewRefreshToken;

            user.RefreshTokenExpiry =
                DateTime.UtcNow.AddDays(7);

            await _userService.UpdateUserAsync(user);

            return Ok(new
            {
                Message = "Token refreshed successfully.",
                AccessToken = accessToken,
                RefreshToken = newRefreshToken,
                Username = user.Username,
                Role = user.Role
            });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout(
    RefreshTokenDto refreshTokenDto)
        {
            // Hash incoming refresh token
            var hashedRefreshToken =
                _jwtService.HashRefreshToken(
                    refreshTokenDto.RefreshToken);

            // Find user using hashed refresh token
            var user = await _userService
                .GetByRefreshTokenAsync(
                    hashedRefreshToken);

            // Token not found
            if (user == null)
            {
                return Unauthorized(new
                {
                    Message = "Invalid refresh token."
                });
            }

            // Revoke refresh token
            user.RefreshToken = null;
            user.RefreshTokenExpiry = null;

            await _userService.UpdateUserAsync(user);

            return Ok(new
            {
                Message = "Logout successful."
            });
        }
    }
}
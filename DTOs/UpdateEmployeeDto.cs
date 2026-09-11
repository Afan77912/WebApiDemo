using System.ComponentModel.DataAnnotations;

namespace WebApiDemo.DTOs
{
    public class UpdateEmployeeDto
    {
        [Required(ErrorMessage = "Employee name is required.")]
        [MinLength(3, ErrorMessage = "Name must contain at least 3 characters.")]
        public string Name { get; set; }
        
        public string Department { get; set; }

        [Range(18, 60, ErrorMessage = "Age must be between 18 and 60.")]
        public int Age { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Phone number is required.")]
        [Phone(ErrorMessage = "Please enter a valid phone number.")]
        [RegularExpression(
    @"^[6-9]\d{9}$",
    ErrorMessage = "Enter a valid 10-digit Indian mobile number."
)]
        public string Phone { get; set; }
    }
}
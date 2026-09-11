using System.ComponentModel.DataAnnotations;

namespace WebApiDemo.DTOs
{
    public class CreateEmployeeDto
    {
        [Required(ErrorMessage = "Employee name is required.")]
        [StringLength(50, MinimumLength = 2,
            ErrorMessage = "Name must be between 2 and 50 characters.")]
        [MinLength(3, ErrorMessage = "Name must contain at least 3 characters.")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Department is required.")]
        [StringLength(50, MinimumLength = 2,
            ErrorMessage = "Department must be between 2 and 50 characters.")]
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
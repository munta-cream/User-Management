// File: Models/User.cs
using System.ComponentModel.DataAnnotations;

namespace Demo.Models
{
    /// <summary>
    /// User status enumeration for account management
    /// </summary>
    public enum UserStatus 
    { 
        /// <summary>Active user can login and access the system</summary>
        Active, 
        
        /// <summary>Blocked user cannot login</summary>
        Blocked, 
        
        /// <summary>Deleted user is marked for removal</summary>
        Deleted 
    }

    /// <summary>
    /// User entity for the user management system
    /// </summary>
    public class User
    {
        /// <summary>
        /// Primary key for the user
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// User's full name
        /// </summary>
        [Required(ErrorMessage = "Name is required")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 100 characters")]
        [Display(Name = "Full Name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// User's date of birth
        /// </summary>
        [Required(ErrorMessage = "Date of birth is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Date of Birth")]
        public DateTime DateOfBirth { get; set; }

        /// <summary>
        /// User's mobile phone number
        /// </summary>
        [Required(ErrorMessage = "Mobile number is required")]
        [StringLength(20, MinimumLength = 10, ErrorMessage = "Mobile number must be between 10 and 20 characters")]
        [Phone(ErrorMessage = "Invalid mobile number format")]
        [Display(Name = "Mobile Number")]
        public string MobileNumber { get; set; } = string.Empty;

        /// <summary>
        /// User's email address (unique identifier)
        /// </summary>
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address format")]
        [StringLength(100, ErrorMessage = "Email must not exceed 100 characters")]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// User's gender
        /// </summary>
        [Required(ErrorMessage = "Gender is required")]
        [StringLength(10, ErrorMessage = "Gender must not exceed 10 characters")]
        [Display(Name = "Gender")]
        public string Gender { get; set; } = string.Empty;

        /// <summary>
        /// Hashed password for authentication
        /// </summary>
        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// Last login timestamp for activity tracking
        /// </summary>
        [Display(Name = "Last Login")]
        public DateTime LastLogin { get; set; }

        /// <summary>
        /// Current status of the user account
        /// </summary>
        [Display(Name = "Account Status")]
        public UserStatus Status { get; set; } = UserStatus.Active;

        /// <summary>
        /// When the user account was created
        /// </summary>
        [Display(Name = "Registration Date")]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Last time the user record was updated
        /// </summary>
        [Display(Name = "Last Updated")]
        public DateTime UpdatedAt { get; set; }

        // Helper methods for display
        
        /// <summary>
        /// Get formatted name for display
        /// </summary>
        public string DisplayName => Name?.Trim() ?? "Unknown User";

        /// <summary>
        /// Get status badge CSS class
        /// </summary>
        public string StatusBadgeClass => Status switch
        {
            UserStatus.Active => "bg-success",
            UserStatus.Blocked => "bg-warning text-dark", 
            UserStatus.Deleted => "bg-danger",
            _ => "bg-secondary"
        };

        /// <summary>
        /// Get status icon CSS class
        /// </summary>
        public string StatusIconClass => Status switch
        {
            UserStatus.Active => "fas fa-check-circle",
            UserStatus.Blocked => "fas fa-ban",
            UserStatus.Deleted => "fas fa-trash",
            _ => "fas fa-question-circle"
        };

        /// <summary>
        /// Check if user can login
        /// </summary>
        public bool CanLogin => Status == UserStatus.Active;

        /// <summary>
        /// Get age from date of birth
        /// </summary>
        public int Age
        {
            get
            {
                var today = DateTime.Today;
                var age = today.Year - DateOfBirth.Year;
                if (DateOfBirth.Date > today.AddYears(-age)) age--;
                return age;
            }
        }

        /// <summary>
        /// Get masked mobile number for display
        /// </summary>
        public string MaskedMobileNumber
        {
            get
            {
                if (string.IsNullOrEmpty(MobileNumber) || MobileNumber.Length < 4)
                    return MobileNumber;
                
                return $"****{MobileNumber[^4..]}";
            }
        }

        /// <summary>
        /// Get days since last login
        /// </summary>
        public int DaysSinceLastLogin => (DateTime.UtcNow - LastLogin).Days;

        /// <summary>
        /// Check if user is recently active (logged in within last 7 days)
        /// </summary>
        public bool IsRecentlyActive => DaysSinceLastLogin <= 7;
    }
}
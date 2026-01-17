using System.ComponentModel.DataAnnotations;

namespace TripWings.Models;

public class SiteComment
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "User")]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5")]
    [Display(Name = "Rating")]
    public int Rating { get; set; }

    [Required]
    [StringLength(2000, ErrorMessage = "Comment cannot exceed 2000 characters")]
    [Display(Name = "Comment")]
    public string CommentText { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.DateTime)]
    [Display(Name = "Created At")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [DataType(DataType.DateTime)]
    [Display(Name = "Updated At")]
    public DateTime? UpdatedAt { get; set; }

    public bool IsApproved { get; set; } = true;

    public ApplicationUser User { get; set; } = null!;
    public ICollection<CommentRating> Ratings { get; set; } = new List<CommentRating>();
}

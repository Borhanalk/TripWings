using System.ComponentModel.DataAnnotations;

namespace TripWings.Models;

public class CommentRating
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Comment")]
    public int SiteCommentId { get; set; }

    [Required]
    [Display(Name = "User")]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Is Helpful")]
    public bool IsHelpful { get; set; } = true;

    [Required]
    [DataType(DataType.DateTime)]
    [Display(Name = "Created At")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public SiteComment SiteComment { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}

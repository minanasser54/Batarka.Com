using System.ComponentModel.DataAnnotations;

namespace MyMvcApp10.Models;

public class GalleryEvent
{
    public int Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<GalleryImage> Images { get; set; } = new List<GalleryImage>();
}

public class GalleryImage
{
    public int Id { get; set; }

    public int GalleryEventId { get; set; }
    public GalleryEvent GalleryEvent { get; set; } = null!;

    [Required]
    [MaxLength(255)]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(40)]
    public string ContentType { get; set; } = string.Empty;

    public int FileSize { get; set; }

    [Required]
    public byte[] ImageData { get; set; } = Array.Empty<byte>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class GalleryEventViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<GalleryImageViewModel> Images { get; set; } = new();
}

public class GalleryImageViewModel
{
    public int Id { get; set; }
    public int GalleryEventId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
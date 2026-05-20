using System.ComponentModel.DataAnnotations;

namespace STRAIBot.Models;

public class DraftResponseRequest
{
    [Required(ErrorMessage = "propertyName is required.")]
    public string PropertyName { get; set; } = string.Empty;

    [Required(ErrorMessage = "guestMessage is required.")]
    public string GuestMessage { get; set; } = string.Empty;
}

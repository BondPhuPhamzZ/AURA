using System.ComponentModel.DataAnnotations;
using AURA.Models;

namespace AURA.ViewModels;

/// <summary>
/// Submitter context posted with each web receipt upload. The rules live here only:
/// tag helpers render the matching HTML attributes and the controller checks ModelState.
/// </summary>
public sealed class ReceiptSubmitterInput
{
    private const string RequiredMessage = "Vui lòng nhập {0}.";
    private const string LengthMessage = "{0} không được vượt quá {1} ký tự.";

    [Display(Name = "Mã nhân viên")]
    [Required(ErrorMessage = RequiredMessage)]
    [StringLength(ReimbursementRequest.SubmitterCodeMaxLength, ErrorMessage = LengthMessage)]
    public string? SubmitterCode { get; set; }

    [Display(Name = "Người gửi")]
    [Required(ErrorMessage = RequiredMessage)]
    [StringLength(ReimbursementRequest.SubmitterDisplayNameMaxLength, ErrorMessage = LengthMessage)]
    public string? SubmitterDisplayName { get; set; }

    [Display(Name = "Bộ phận")]
    [Required(ErrorMessage = RequiredMessage)]
    [StringLength(ReimbursementRequest.SubmitterDepartmentMaxLength, ErrorMessage = LengthMessage)]
    public string? SubmitterDepartment { get; set; }

    /// <summary>Pre-filled values for the supervised synthetic demo form.</summary>
    public static ReceiptSubmitterInput CreateDemo() => new()
    {
        SubmitterCode = "EMP-001",
        SubmitterDisplayName = "Gia Phú",
        SubmitterDepartment = "Vận hành"
    };
}

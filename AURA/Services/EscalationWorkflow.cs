namespace AURA.Services;

public static class EscalationWorkflow
{
    public const string EmployeeHandoffPrompt =
        "Bạn có xác nhận chuyển tiếp hồ sơ này đến cấp trên để ra quyết định không?";

    private static readonly HashSet<string> ResolvedStatuses =
    [
        "MANUAL_REVIEW_ACCEPTED",
        "RETURNED_FOR_MORE_EVIDENCE",
        "APPROVED_POLICY_EXCEPTION",
        "REJECTED_POLICY_EXCEPTION",
        "FORWARDED_TO_AUTHORITY",
        "RETURNED_BY_MANAGER",
        "RETURNED_FOR_RETRY"
    ];

    public static (string Status, string AuditAction, string Message) Answer(string escalationStatus, bool answer)
    {
        if (!PolicyDecisionEngine.IsEscalation(escalationStatus))
            throw new InvalidOperationException("Hồ sơ không ở trạng thái chờ quyết định.");

        return (escalationStatus, answer) switch
        {
            ("ESCALATE_POLICY", true) => ("APPROVED_POLICY_EXCEPTION", "MANAGER_YES",
                "Quản lý đồng ý duyệt: ngoại lệ chính sách đã được phê duyệt."),
            ("ESCALATE_POLICY", false) => ("REJECTED_POLICY_EXCEPTION", "MANAGER_NO",
                "Quản lý từ chối duyệt: khoản chi ngoài chính sách đã bị từ chối."),
            ("ESCALATE_AUTHORITY", true) => ("FORWARDED_TO_AUTHORITY", "MANAGER_YES",
                "Quản lý đồng ý duyệt: hồ sơ đã được chuyển lên cấp có thẩm quyền."),
            ("ESCALATE_AUTHORITY", false) => ("RETURNED_BY_MANAGER", "MANAGER_NO",
                "Quản lý từ chối duyệt: hồ sơ vượt thẩm quyền đã được trả lại."),
            ("ESCALATE_FACT", true) => ("MANUAL_REVIEW_ACCEPTED", "MANAGER_YES",
                "Quản lý đồng ý duyệt: hồ sơ được tiếp nhận để kiểm tra thủ công."),
            ("ESCALATE_FACT", false) => ("RETURNED_FOR_MORE_EVIDENCE", "MANAGER_NO",
                "Quản lý từ chối duyệt: hồ sơ được trả lại để bổ sung chứng từ."),
            ("ESCALATE_SYSTEM_ERROR", true) => ("MANUAL_REVIEW_ACCEPTED", "MANAGER_YES",
                "Quản lý đồng ý duyệt: hồ sơ lỗi hệ thống được tiếp nhận để kiểm tra thủ công."),
            ("ESCALATE_SYSTEM_ERROR", false) => ("RETURNED_FOR_RETRY", "MANAGER_NO",
                "Quản lý từ chối duyệt: hồ sơ được trả lại để thử xử lý lại sau."),
            _ => answer
                ? ("MANUAL_REVIEW_ACCEPTED", "MANAGER_YES", "Quản lý đồng ý duyệt: hồ sơ được xử lý thủ công.")
                : ("RETURNED_BY_MANAGER", "MANAGER_NO", "Quản lý từ chối duyệt: hồ sơ được trả lại.")
        };
    }

    public static bool CanUndo(string? status) =>
        !string.IsNullOrWhiteSpace(status) && ResolvedStatuses.Contains(status);

    public static (string Yes, string No) ExplainChoices(string? escalationStatus) => escalationStatus switch
    {
        "ESCALATE_POLICY" => ("Đồng ý duyệt ngoại lệ chính sách", "Từ chối khoản chi ngoài chính sách"),
        "ESCALATE_AUTHORITY" => ("Đồng ý chuyển cấp có thẩm quyền", "Từ chối và trả lại hồ sơ"),
        "ESCALATE_SYSTEM_ERROR" => ("Đồng ý tiếp nhận kiểm tra thủ công", "Từ chối và trả lại để thử sau"),
        _ => ("Đồng ý tiếp nhận kiểm tra thủ công", "Từ chối và yêu cầu bổ sung chứng từ")
    };
}

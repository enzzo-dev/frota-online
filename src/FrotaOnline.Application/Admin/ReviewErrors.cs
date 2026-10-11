using FrotaOnline.Application.Common;

namespace FrotaOnline.Application.Admin;

public static class ReviewErrors
{
    public static readonly Error DriverNotFound =
        new("admin.driver_not_found", "Driver not found.", ErrorType.NotFound);

    public static readonly Error FileNotFound =
        new("admin.file_not_found", "File not found for this driver.", ErrorType.NotFound);

    public static readonly Error NotPendingReview =
        new("admin.not_pending_review", "Only drivers pending review can be approved or rejected.", ErrorType.Conflict);

    public static readonly Error IncompleteFile =
        new("admin.incomplete_file", "The driver has no active vehicle or no document. Reject it so the driver can fix the file.", ErrorType.Conflict);
}

using FrotaOnline.Domain.Common;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Domain.Entities;

public sealed class DriverProfile : BaseEntity, IAggregateRoot
{
    private DriverProfile()
    {
    }

    public Guid UserId { get; private set; }

    public string CpfNumber { get; private set; } = string.Empty;

    public string CnhNumber { get; private set; } = string.Empty;

    public string CnhCategory { get; private set; } = string.Empty;

    public DriverStatus Status { get; private set; } = DriverStatus.Draft;

    public string? RejectionReason { get; private set; }

    public DateTime? ReviewedAt { get; private set; }

    public Guid? ReviewedByUserId { get; private set; }

    public bool CanOperate => Status == DriverStatus.Approved;

    public static DriverProfile Create(Guid userId, string cpfNumber, string cnhNumber, string cnhCategory) => new()
    {
        UserId = Guard.NotEmpty(userId, nameof(userId)),
        CpfNumber = Guard.NotNullOrWhiteSpace(cpfNumber, nameof(cpfNumber)),
        CnhNumber = Guard.NotNullOrWhiteSpace(cnhNumber, nameof(cnhNumber)),
        CnhCategory = Guard.NotNullOrWhiteSpace(cnhCategory, nameof(cnhCategory)).ToUpperInvariant()
    };

    public bool IsEditable => Status is DriverStatus.Draft or DriverStatus.Rejected;

    public void UpdateDetails(string cpfNumber, string cnhNumber, string cnhCategory)
    {
        if (!IsEditable)
        {
            throw new DomainException($"A driver in status {Status} cannot change their details.");
        }

        CpfNumber = Guard.NotNullOrWhiteSpace(cpfNumber, nameof(cpfNumber));
        CnhNumber = Guard.NotNullOrWhiteSpace(cnhNumber, nameof(cnhNumber));
        CnhCategory = Guard.NotNullOrWhiteSpace(cnhCategory, nameof(cnhCategory)).ToUpperInvariant();
    }

    public void SubmitForReview()
    {
        if (Status is not (DriverStatus.Draft or DriverStatus.Rejected))
        {
            throw new DomainException($"A driver in status {Status} cannot be submitted for review.");
        }

        Status = DriverStatus.PendingReview;
        RejectionReason = null;
    }

    public void Approve(Guid reviewerId, DateTime utcNow)
    {
        EnsurePendingReview();
        Status = DriverStatus.Approved;
        ReviewedByUserId = Guard.NotEmpty(reviewerId, nameof(reviewerId));
        ReviewedAt = utcNow;
    }

    public void Reject(Guid reviewerId, string reason, DateTime utcNow)
    {
        EnsurePendingReview();
        Status = DriverStatus.Rejected;
        RejectionReason = Guard.NotNullOrWhiteSpace(reason, nameof(reason));
        ReviewedByUserId = Guard.NotEmpty(reviewerId, nameof(reviewerId));
        ReviewedAt = utcNow;
    }

    private void EnsurePendingReview()
    {
        if (Status != DriverStatus.PendingReview)
        {
            throw new DomainException($"Only drivers pending review can be reviewed (current status: {Status}).");
        }
    }
}

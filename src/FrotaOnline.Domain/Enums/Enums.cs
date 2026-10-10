namespace FrotaOnline.Domain.Enums;

public enum UserRole { Customer = 1, Driver = 2, Admin = 3 }

public enum DriverStatus { Draft = 1, PendingReview = 2, Approved = 3, Rejected = 4 }

public enum VehicleType { Fiorino = 1, Van = 2, Pickup = 3, Truck34 = 4, Truck = 5, Trailer = 6 }

/// <summary>What happens at a stop: the driver collects cargo or hands it over.</summary>
public enum StopType { Pickup = 1, Delivery = 2 }

public enum FreightStatus { Open = 1, Approved = 2, Expired = 3, Cancelled = 4 }

public enum ProposalStatus { Pending = 1, Accepted = 2, Rejected = 3, Expired = 4, Withdrawn = 5 }

public enum DeliveryStatus
{
    Scheduled = 1,
    ToPickup = 2,
    PickedUp = 3,
    InTransit = 4,
    Delivered = 5,
    Cancelled = 6,
    Incident = 7
}

public enum PaymentMethod { Pix = 1, Cash = 2, Card = 3, Other = 4 }

public enum PaymentStatus { Pending = 1, Paid = 2, Failed = 3 }

public enum FeeEntryType { FeeAccrued = 1, FeeSettled = 2 }

public enum SubscriptionPlan { Basic = 1, Full = 2 }

public enum SubscriptionStatus { Pending = 1, Active = 2, Cancelled = 3 }

public enum MediaKind { DriverDocument = 1, VehiclePhoto = 2, CargoPhoto = 3, DeliveryProof = 4 }

public enum DevicePlatform { Android = 1, Ios = 2, Web = 3 }

public enum TermType { TermsOfUse = 1, PrivacyPolicy = 2 }

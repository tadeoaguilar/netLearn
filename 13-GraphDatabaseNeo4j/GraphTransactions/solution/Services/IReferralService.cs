namespace GraphTransactions.Services;

// A referral spans three separate graph writes -- a WORKS_AT relationship
// for the new hire, a KNOWS relationship between the referrer and the new
// hire, and an increment of the referrer's referralCount -- that must all
// succeed or none should. This interface is the unit-of-work boundary:
// callers ask for one referral and never see the individual transaction
// calls underneath, mirroring 10-EntityFrameworkCore/EfCoreTransactions's
// ITransferService/IUnitOfWork split.
public interface IReferralService
{
    Task<ReferralResult> ReferAsync(
        string referrerId,
        string newHireId,
        string companyId,
        string role,
        bool simulateFailureBeforeIncrement = false);
}

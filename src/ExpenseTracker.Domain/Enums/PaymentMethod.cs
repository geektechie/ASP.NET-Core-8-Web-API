namespace ExpenseTracker.Domain.Enums;

/// <summary>
/// How an expense was paid.
/// <para>
/// Values are assigned explicitly and never renumbered: the number is what sits in
/// the database, so reordering this list would silently relabel historic rows.
/// </para>
/// </summary>
public enum PaymentMethod
{
    Cash = 1,
    DebitCard = 2,
    CreditCard = 3,
    BankTransfer = 4,
    DirectDebit = 5,
    DigitalWallet = 6,
    Other = 99
}

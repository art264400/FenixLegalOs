using Microsoft.Extensions.Logging;

namespace FenixLegalOs.Services;

/// <summary>
/// Стабильные идентификаторы событий (EventId) для структурированного логирования платёжной подсистемы.
/// </summary>
public static class PaymentEvents
{
    // PaymentService (1000-1099)
    public static readonly EventId PaymentStartRequested = new(1000, nameof(PaymentStartRequested));
    public static readonly EventId PaymentCreated = new(1001, nameof(PaymentCreated));
    public static readonly EventId PaymentRejected = new(1002, nameof(PaymentRejected));
    public static readonly EventId PaymentActiveAttemptReopened = new(1003, nameof(PaymentActiveAttemptReopened));
    public static readonly EventId PaymentStartCompleted = new(1004, nameof(PaymentStartCompleted));
    public static readonly EventId PaymentError = new(1005, nameof(PaymentError));

    // BccPaymentGateway (1100-1199)
    public static readonly EventId BccFormPreparing = new(1100, nameof(BccFormPreparing));
    public static readonly EventId BccFormPrepared = new(1101, nameof(BccFormPrepared));
    public static readonly EventId BccGatewayWarning = new(1102, nameof(BccGatewayWarning));
    public static readonly EventId BccGatewayError = new(1103, nameof(BccGatewayError));
    public static readonly EventId BccTestConfigurationLoaded = new(1104, nameof(BccTestConfigurationLoaded));

    // BccNotificationService & Callback (1200-1299)
    public static readonly EventId BccCallbackReceived = new(1200, nameof(BccCallbackReceived));
    public static readonly EventId BccCallbackRejected = new(1201, nameof(BccCallbackRejected));
    public static readonly EventId PaymentStatusChanged = new(1202, nameof(PaymentStatusChanged));
    public static readonly EventId BccCallbackCompleted = new(1203, nameof(BccCallbackCompleted));
    public static readonly EventId BccCallbackError = new(1204, nameof(BccCallbackError));
    public static readonly EventId BccUnauthenticatedTestCallbackAccepted = new(1205, nameof(BccUnauthenticatedTestCallbackAccepted));
    public static readonly EventId BccTestAuthorizationCaptured = new(1206, nameof(BccTestAuthorizationCaptured));

    // BccCallbacksController (1300-1399)
    public static readonly EventId BccNotifyRequestReceived = new(1300, nameof(BccNotifyRequestReceived));
    public static readonly EventId BccNotifyRequestProcessed = new(1301, nameof(BccNotifyRequestProcessed));
    public static readonly EventId BccReturnReceived = new(1302, nameof(BccReturnReceived));
    public static readonly EventId BccControllerWarning = new(1303, nameof(BccControllerWarning));
    public static readonly EventId BccControllerError = new(1304, nameof(BccControllerError));

    // PaymentRefundService & Admin Refunds (1400-1499)
    public static readonly EventId PaymentRefundRequested = new(1400, nameof(PaymentRefundRequested));
    public static readonly EventId PaymentRefundRejected = new(1401, nameof(PaymentRefundRejected));
    public static readonly EventId PaymentRefundGatewaySent = new(1402, nameof(PaymentRefundGatewaySent));
    public static readonly EventId PaymentRefundGatewayAccepted = new(1403, nameof(PaymentRefundGatewayAccepted));
    public static readonly EventId PaymentRefundAwaitingCallback = new(1404, nameof(PaymentRefundAwaitingCallback));
    public static readonly EventId PaymentRefundSucceeded = new(1405, nameof(PaymentRefundSucceeded));
    public static readonly EventId PaymentRefundFailed = new(1406, nameof(PaymentRefundFailed));
    public static readonly EventId PaymentRefundDuplicateCallback = new(1407, nameof(PaymentRefundDuplicateCallback));
}

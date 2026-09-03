namespace Workforce.Contracts;

/// <summary>
/// A work order exactly as the workforce system holds it: the operational content that explains an
/// asset's state, and alongside it the commercial and personal detail that must never leave the
/// domain - who was dispatched, what they are paid, what the visit was billed at.
/// <para>
/// This record is deliberately the <em>whole</em> thing. Nothing outside the workforce domain ever
/// receives it; the domain's own agent does not receive it either. It exists so the demo can show
/// what was withheld, side by side with what crossed.
/// </para>
/// </summary>
/// <param name="WorkOrderId">The stable work order identifier.</param>
/// <param name="AssetId">The asset the work order concerns.</param>
/// <param name="Title">A short operational title.</param>
/// <param name="Reason">Why the work order exists - shareable operational content.</param>
/// <param name="Status">Where the work order has got to - shareable.</param>
/// <param name="RaisedAt">When the work order was raised - shareable.</param>
/// <param name="ExpectedClearanceAt">When the asset is expected to return to normal - shareable.</param>
/// <param name="TechnicianName">The dispatched technician - personal data, never shareable.</param>
/// <param name="TechnicianBadge">The technician's badge number - personal data, never shareable.</param>
/// <param name="LabourCost">What the visit cost - commercial data, never shareable.</param>
/// <param name="CallOutRate">The contracted call-out rate applied - commercial data, never shareable.</param>
/// <param name="TechnicianNote">
/// The technician's note as written: operational content and commercial content in one paragraph,
/// the way a real work order arrives. Never shareable, and never selected by the extraction tool.
/// </param>
/// <param name="OperationalSummary">
/// The operational part of the visit, recorded as its own field by the workforce system.
/// <para>
/// That separation is the design lesson, not an accident of the fixture: a domain that intends to
/// answer other domains' questions has to make the shareable part separable at write time. Keep
/// only the entangled note and there is no safe way to share it - you would be back to asking a
/// model to decide which half of a paragraph may leave, with the whole paragraph in its context.
/// </para>
/// </param>
public sealed record WorkOrderRecord(
    string WorkOrderId,
    string AssetId,
    string Title,
    string Reason,
    string Status,
    DateTimeOffset RaisedAt,
    DateTimeOffset? ExpectedClearanceAt,
    string TechnicianName,
    string TechnicianBadge,
    decimal LabourCost,
    string CallOutRate,
    string TechnicianNote,
    string OperationalSummary);

/// <summary>
/// The work order as anyone outside the workforce domain may see it.
/// <para>
/// This is the type the domain's extraction tool returns, and it is the only shape the workforce
/// agent ever has in its context. The commercial and personal fields are not redacted here - they
/// are simply absent, because they were never selected. An agent cannot disclose what it does not
/// hold, so no instruction from a caller can talk it into revealing them.
/// </para>
/// </summary>
/// <param name="WorkOrderId">The stable work order identifier.</param>
/// <param name="AssetId">The asset the work order concerns.</param>
/// <param name="Title">A short operational title.</param>
/// <param name="Reason">Why the work order exists.</param>
/// <param name="Status">Where the work order has got to.</param>
/// <param name="RaisedAt">When the work order was raised.</param>
/// <param name="ExpectedClearanceAt">When the asset is expected to return to normal.</param>
/// <param name="OperationalNote">The operational part of the technician's note, and only that part.</param>
public sealed record ShareableWorkOrderDetails(
    string WorkOrderId,
    string AssetId,
    string Title,
    string Reason,
    string Status,
    DateTimeOffset RaisedAt,
    DateTimeOffset? ExpectedClearanceAt,
    string OperationalNote);

/// <summary>
/// A work order as it appears in a search result: enough to choose which one to open, and nothing
/// that would make the choice itself a disclosure.
/// </summary>
/// <param name="WorkOrderId">The stable work order identifier.</param>
/// <param name="AssetId">The asset the work order concerns.</param>
/// <param name="Title">A short operational title.</param>
/// <param name="Status">Where the work order has got to.</param>
/// <param name="RaisedAt">When the work order was raised.</param>
public sealed record WorkOrderSummary(
    string WorkOrderId,
    string AssetId,
    string Title,
    string Status,
    DateTimeOffset RaisedAt);


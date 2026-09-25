using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Repository;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Apps;

namespace Reporting.Api.Services.Sources.Hrms;

// 31. Recruitment Pipeline
public class RecruitmentPipelineRow
{
    public long Id { get; set; }
    public string RequisitionCode { get; set; } = null!;
    public string OpeningTitle { get; set; } = null!;
    public string CandidateName { get; set; } = null!;
    public string CandidateEmail { get; set; } = null!;
    public string Stage { get; set; } = null!;
    public string CandidateSource { get; set; } = null!;
}

public sealed class RecruitmentPipelineSource : ReportSource<RecruitmentPipelineRow>
{
    public override string ReportKey => "recruitment-pipeline";
    public override string Title => "Recruitment Pipeline by Stage";
    public override ReportModule Module => ReportModule.Recruitment;
    public override App App => App.Hrms;
    public override string RequiredPermission => "recruitment.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<RecruitmentPipelineRow, string>("requisitionCode", ColumnDataType.Text, r => r.RequisitionCode, groupable: true),
        ReportColumn.Of<RecruitmentPipelineRow, string>("openingTitle", ColumnDataType.Text, r => r.OpeningTitle, groupable: true),
        ReportColumn.Of<RecruitmentPipelineRow, string>("candidateName", ColumnDataType.Text, r => r.CandidateName, groupable: true),
        ReportColumn.Of<RecruitmentPipelineRow, string>("candidateEmail", ColumnDataType.Text, r => r.CandidateEmail),
        ReportColumn.Of<RecruitmentPipelineRow, string>("stage", ColumnDataType.Text, r => r.Stage, groupable: true),
        ReportColumn.Of<RecruitmentPipelineRow, string>("candidateSource", ColumnDataType.Text, r => r.CandidateSource, groupable: true),
        ReportColumn.Of<RecruitmentPipelineRow, long>("id", ColumnDataType.Number, r => r.Id, filterable: false),
    ];

    protected override IQueryable<RecruitmentPipelineRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from a in db.Applications
               join o in db.JobOpenings on a.JobOpeningId equals o.JobOpeningId
               join req in db.JobRequisitions on o.JobRequisitionId equals req.JobRequisitionId
               join c in db.Candidates on a.CandidateId equals c.CandidateId
               select new RecruitmentPipelineRow
               {
                   Id = a.ApplicationId,
                   RequisitionCode = req.RequisitionCode,
                   OpeningTitle = o.Title,
                   CandidateName = c.FirstName + (c.LastName != null ? " " + c.LastName : ""),
                   CandidateEmail = c.Email,
                   Stage = a.Stage,
                   CandidateSource = c.CandidateSource,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<RecruitmentPipelineRow, string>>)(r => r.RequisitionCode);
}

// 32. Time to Hire
public class TimeToHireRow
{
    public long Id { get; set; }
    public string RequisitionCode { get; set; } = null!;
    public string OpeningTitle { get; set; } = null!;
    public string CandidateName { get; set; } = null!;
    public decimal OfferedCtc { get; set; }
    public DateOnly JoiningDate { get; set; }
    public string OfferStatus { get; set; } = null!;
}

public sealed class TimeToHireSource : ReportSource<TimeToHireRow>
{
    public override string ReportKey => "time-to-hire";
    public override string Title => "Time to Hire";
    public override ReportModule Module => ReportModule.Recruitment;
    public override App App => App.Hrms;
    public override string RequiredPermission => "recruitment.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<TimeToHireRow, string>("requisitionCode", ColumnDataType.Text, r => r.RequisitionCode, groupable: true),
        ReportColumn.Of<TimeToHireRow, string>("openingTitle", ColumnDataType.Text, r => r.OpeningTitle, groupable: true),
        ReportColumn.Of<TimeToHireRow, string>("candidateName", ColumnDataType.Text, r => r.CandidateName, groupable: true),
        ReportColumn.Of<TimeToHireRow, decimal>("offeredCtc", ColumnDataType.Money, r => r.OfferedCtc, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<TimeToHireRow, DateOnly>("joiningDate", ColumnDataType.Date, r => r.JoiningDate),
        ReportColumn.Of<TimeToHireRow, string>("offerStatus", ColumnDataType.Text, r => r.OfferStatus, groupable: true),
        ReportColumn.Of<TimeToHireRow, long>("id", ColumnDataType.Number, r => r.Id, filterable: false),
    ];

    protected override IQueryable<TimeToHireRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from off in db.Offers
               join a in db.Applications on off.ApplicationId equals a.ApplicationId
               join o in db.JobOpenings on a.JobOpeningId equals o.JobOpeningId
               join req in db.JobRequisitions on o.JobRequisitionId equals req.JobRequisitionId
               join c in db.Candidates on a.CandidateId equals c.CandidateId
               select new TimeToHireRow
               {
                   Id = off.OfferId,
                   RequisitionCode = req.RequisitionCode,
                   OpeningTitle = o.Title,
                   CandidateName = c.FirstName + (c.LastName != null ? " " + c.LastName : ""),
                   OfferedCtc = off.OfferedCtc,
                   JoiningDate = off.JoiningDate,
                   OfferStatus = off.OfferStatus,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<TimeToHireRow, DateOnly>>)(r => r.JoiningDate);
}

// 33. Source Effectiveness
public class SourceEffectivenessRow
{
    public string CandidateSource { get; set; } = null!;
    public int CandidateCount { get; set; }
}

public sealed class SourceEffectivenessSource : ReportSource<SourceEffectivenessRow>
{
    public override string ReportKey => "source-effectiveness";
    public override string Title => "Source Effectiveness";
    public override ReportModule Module => ReportModule.Recruitment;
    public override App App => App.Hrms;
    public override string RequiredPermission => "recruitment.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<SourceEffectivenessRow, string>("candidateSource", ColumnDataType.Text, r => r.CandidateSource, groupable: true),
        ReportColumn.Of<SourceEffectivenessRow, int>("candidateCount", ColumnDataType.Quantity, r => r.CandidateCount, aggregate: AggregateFunction.Sum),
    ];

    protected override IQueryable<SourceEffectivenessRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from c in db.Candidates
               group c by c.CandidateSource into grp
               select new SourceEffectivenessRow
               {
                   CandidateSource = grp.Key,
                   CandidateCount = grp.Count(),
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<SourceEffectivenessRow, string>>)(r => r.CandidateSource);
}

// 34. Offer Acceptance
public class OfferAcceptanceRow
{
    public long Id { get; set; }
    public string CandidateName { get; set; } = null!;
    public string CandidateEmail { get; set; } = null!;
    public decimal OfferedCtc { get; set; }
    public DateOnly JoiningDate { get; set; }
    public string OfferStatus { get; set; } = null!;
    public string ApprovalStatus { get; set; } = null!;
}

public sealed class OfferAcceptanceSource : ReportSource<OfferAcceptanceRow>
{
    public override string ReportKey => "offer-acceptance";
    public override string Title => "Offer Acceptance";
    public override ReportModule Module => ReportModule.Recruitment;
    public override App App => App.Hrms;
    public override string RequiredPermission => "recruitment.view";

    public override IReadOnlyList<ReportColumn> Columns =>
    [
        ReportColumn.Of<OfferAcceptanceRow, string>("candidateName", ColumnDataType.Text, r => r.CandidateName, groupable: true),
        ReportColumn.Of<OfferAcceptanceRow, string>("candidateEmail", ColumnDataType.Text, r => r.CandidateEmail),
        ReportColumn.Of<OfferAcceptanceRow, decimal>("offeredCtc", ColumnDataType.Money, r => r.OfferedCtc, aggregate: AggregateFunction.Sum),
        ReportColumn.Of<OfferAcceptanceRow, DateOnly>("joiningDate", ColumnDataType.Date, r => r.JoiningDate),
        ReportColumn.Of<OfferAcceptanceRow, string>("offerStatus", ColumnDataType.Text, r => r.OfferStatus, groupable: true),
        ReportColumn.Of<OfferAcceptanceRow, string>("approvalStatus", ColumnDataType.Text, r => r.ApprovalStatus, groupable: true),
        ReportColumn.Of<OfferAcceptanceRow, long>("id", ColumnDataType.Number, r => r.Id, filterable: false),
    ];

    protected override IQueryable<OfferAcceptanceRow> Build(ReportParameters parameters, ReportingDbContext db)
    {
        return from off in db.Offers
               join a in db.Applications on off.ApplicationId equals a.ApplicationId
               join c in db.Candidates on a.CandidateId equals c.CandidateId
               select new OfferAcceptanceRow
               {
                   Id = off.OfferId,
                   CandidateName = c.FirstName + (c.LastName != null ? " " + c.LastName : ""),
                   CandidateEmail = c.Email,
                   OfferedCtc = off.OfferedCtc,
                   JoiningDate = off.JoiningDate,
                   OfferStatus = off.OfferStatus,
                   ApprovalStatus = off.ApprovalStatus,
               };
    }

    protected override LambdaExpression DefaultOrder =>
        (Expression<Func<OfferAcceptanceRow, DateOnly>>)(r => r.JoiningDate);
}

// <copyright>
//     Copyright (c) Lukas Grützmacher. All rights reserved.
// </copyright>

namespace lg2de.SimpleAccounting.Reports;

using System;

/// <summary>
///     Defines abstraction for the annual balance report.
/// </summary>
internal interface IAnnualBalanceReport
{
    void CreateReport(AnnualBalanceReport.PeriodCodes period = AnnualBalanceReport.PeriodCodes.None);

    void ShowPreview(string documentName);
}

using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260822150000_AddInvoiceVoidAndCorrectionFields")]
    partial class AddInvoiceVoidAndCorrectionFields
    {
        // Designer.cs minimo (mismo patron que 20260822130000_AddProposalAttachmentAndAuditFields.Designer.cs):
        // no incluye BuildTargetModel ni toca AppDbContextModelSnapshot.cs -- suficiente para
        // que Database.Migrate() descubra y aplique esta migracion en runtime.
    }
}

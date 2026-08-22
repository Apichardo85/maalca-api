using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260822130000_AddProposalAttachmentAndAuditFields")]
    partial class AddProposalAttachmentAndAuditFields
    {
        // Designer.cs minimo (mismo patron que 20260822120000_AddInventoryUnitAndRestrictRecipeDelete.Designer.cs):
        // no incluye BuildTargetModel ni toca AppDbContextModelSnapshot.cs -- suficiente para
        // que Database.Migrate() descubra y aplique esta migracion en runtime.
    }
}

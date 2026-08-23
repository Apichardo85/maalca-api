using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260822170000_AddVirtualBookingFields")]
    partial class AddVirtualBookingFields
    {
        // Designer.cs minimo (mismo patron que 20260822160000_AddInventoryBarcodeAndInternalCode.Designer.cs):
        // no incluye BuildTargetModel ni toca AppDbContextModelSnapshot.cs -- suficiente para
        // que Database.Migrate() descubra y aplique esta migracion en runtime.
    }
}

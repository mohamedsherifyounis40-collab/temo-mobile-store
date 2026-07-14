using Microsoft.Data.Sqlite;
using TemoStore.Core.Abstractions;

namespace TemoStore.Data.Repositories
{
    public class InventoryAdjustmentRepository : IInventoryAdjustmentRepository
    {
        private readonly SqliteConnection _conn;
        private readonly SqliteTransaction _tx;

        public InventoryAdjustmentRepository(SqliteConnection conn, SqliteTransaction tx)
        {
            _conn = conn;
            _tx = tx;
        }

        public void InsertAdjustmentLog(string barcode, string productName, int systemQtyBefore, int countedQty, int difference)
        {
            using var cmd = new SqliteCommand(
                "INSERT INTO InventoryAdjustments (Barcode, ProductName, SystemQuantityBefore, CountedQuantity, Difference, AdjustmentDate) VALUES (@Barcode, @Name, @Before, @Counted, @Diff, @Date)", _conn, _tx);
            cmd.Parameters.AddWithValue("@Barcode", barcode);
            cmd.Parameters.AddWithValue("@Name", productName);
            cmd.Parameters.AddWithValue("@Before", systemQtyBefore);
            cmd.Parameters.AddWithValue("@Counted", countedQty);
            cmd.Parameters.AddWithValue("@Diff", difference);
            cmd.Parameters.AddWithValue("@Date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.ExecuteNonQuery();
        }
    }
}

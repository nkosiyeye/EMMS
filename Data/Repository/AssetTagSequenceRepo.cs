using System.Data;
using EMMS.Service;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EMMS.Data.Repository
{
    public class AssetTagSequenceRepo : IAssetTagSequence
    {
        public const string SequenceName = "AssetTagSeq";
        private readonly ApplicationDbContext _context;

        public AssetTagSequenceRepo(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<long> NextRangeAsync(int count)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));

            var first = new SqlParameter("@first", SqlDbType.Variant) { Direction = ParameterDirection.Output };
            await _context.Database.ExecuteSqlRawAsync(
                $"EXEC sys.sp_sequence_get_range @sequence_name = N'dbo.{SequenceName}', @range_size = @size, @range_first_value = @first OUTPUT",
                new SqlParameter("@size", SqlDbType.BigInt) { Value = (long)count },
                first);

            return Convert.ToInt64(first.Value);
        }
    }
}

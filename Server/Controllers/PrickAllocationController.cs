using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using StallmedManager.Server.Models;
using StallmedManager.Shared.Models;
using System.Data;

namespace StallmedManager.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PrickAllocationController : ControllerBase
    {
        private readonly StallmedContext _context;

        public PrickAllocationController(StallmedContext context)
        {
            _context = context;
        }

        // ---- Allocate (καλεί sp_AllocateStock) ----
        // Χρησιμοποιείται από τη σελίδα Doctor Orders (inline stepper + "Allocate Όλα")
        [HttpPost("allocate")]
        public async Task<ActionResult<AllocateResult>> Allocate([FromBody] AllocateRequest req)
        {
            var connection = (MySqlConnection)_context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "sp_AllocateStock";
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.Add(new MySqlParameter("p_OrderLineID", MySqlDbType.Int64) { Value = req.OrderLineID });
            cmd.Parameters.Add(new MySqlParameter("p_QuantityToAllocate", MySqlDbType.Int32) { Value = req.Quantity });
            cmd.Parameters.Add(new MySqlParameter("p_UserID", MySqlDbType.Int32) { Value = (object?)req.UserID ?? DBNull.Value });

            var pAllocated = new MySqlParameter("p_QuantityActuallyAllocated", MySqlDbType.Int32) { Direction = ParameterDirection.Output };
            cmd.Parameters.Add(pAllocated);

            try
            {
                await cmd.ExecuteNonQueryAsync();
            }
            catch (MySqlException ex)
            {
                return BadRequest(ex.Message);
            }

            return Ok(new AllocateResult { QuantityActuallyAllocated = Convert.ToInt32(pAllocated.Value) });
        }

        // ---- Reverse Allocation (καλεί sp_ReverseAllocation) ----
        // Γενικό εργαλείο ανάκλησης μεμονωμένης δέσμευσης βάσει ID.
        [HttpPost("reverse")]
        public async Task<ActionResult> Reverse([FromBody] ReverseAllocationRequest req)
        {
            var connection = (MySqlConnection)_context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "sp_ReverseAllocation";
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.Add(new MySqlParameter("p_AllocationID", MySqlDbType.Int64) { Value = req.AllocationID });
            cmd.Parameters.Add(new MySqlParameter("p_UserID", MySqlDbType.Int32) { Value = (object?)req.UserID ?? DBNull.Value });
            cmd.Parameters.Add(new MySqlParameter("p_Reason", MySqlDbType.VarChar) { Value = (object?)req.Reason ?? DBNull.Value });

            try
            {
                await cmd.ExecuteNonQueryAsync();
            }
            catch (MySqlException ex)
            {
                return BadRequest(ex.Message);
            }

            return Ok();
        }
    }
}

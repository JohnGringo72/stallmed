using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StallmedManager.Server.Models;
using StallmedManager.Shared.Models;

namespace StallmedManager.Server.Controllers
{
    // Διαχείριση καταλόγου (τύποι προϊόντος με τιμές + αλλεργιογόνα).
    // Χρησιμοποιεί απευθείας τα shared entities ως DTOs -- απλό CRUD χωρίς
    // παράγωγα δεδομένα. Πρόσβαση μόνο για ρόλο admin (σελίδα /catalog-admin).
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "AdminOnly")]
    public class CatalogAdminController : ControllerBase
    {
        private readonly StallmedContext _context;

        public CatalogAdminController(StallmedContext context)
        {
            _context = context;
        }

        // ---- Τύποι προϊόντος (είδη + τιμές) ----

        [HttpGet("producttypes")]
        public async Task<ActionResult<List<ProductType>>> GetProductTypes()
            => Ok(await _context.ProductTypes
                .OrderBy(p => p.Company).ThenBy(p => p.ProductTypeCode)
                .ToListAsync());

        [HttpPost("producttypes")]
        public async Task<ActionResult<ProductType>> CreateProductType([FromBody] ProductType dto)
        {
            var code = dto.ProductTypeCode?.Trim();
            if (string.IsNullOrEmpty(code))
                return BadRequest("Ο κωδικός τύπου είναι υποχρεωτικός.");
            if (await _context.ProductTypes.AnyAsync(p => p.ProductTypeCode == code))
                return BadRequest($"Υπάρχει ήδη τύπος με κωδικό '{code}'.");

            var entity = new ProductType
            {
                ProductTypeCode = code,
                Company = dto.Company?.Trim().ToUpper(),
                Description = dto.Description?.Trim(),
                DescriptionOther = dto.DescriptionOther?.Trim(),
                PublicPrice = dto.PublicPrice,
                ExFactoryPrice = dto.ExFactoryPrice,
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            _context.ProductTypes.Add(entity);
            await _context.SaveChangesAsync();
            return Ok(entity);
        }

        [HttpPut("producttypes/{code}")]
        public async Task<ActionResult<ProductType>> UpdateProductType(string code, [FromBody] ProductType dto)
        {
            var entity = await _context.ProductTypes.FindAsync(code);
            if (entity == null) return NotFound();

            entity.Company = dto.Company?.Trim().ToUpper();
            entity.Description = dto.Description?.Trim();
            entity.DescriptionOther = dto.DescriptionOther?.Trim();
            entity.PublicPrice = dto.PublicPrice;
            entity.ExFactoryPrice = dto.ExFactoryPrice;
            entity.IsActive = dto.IsActive;
            await _context.SaveChangesAsync();
            return Ok(entity);
        }

        // ---- Αλλεργιογόνα ----

        [HttpGet("allergens")]
        public async Task<ActionResult<List<AllergenCode>>> GetAllergens()
            => Ok(await _context.AllergenCodes
                .OrderBy(a => a.CodePrick)
                .ToListAsync());

        [HttpPost("allergens")]
        public async Task<ActionResult<AllergenCode>> CreateAllergen([FromBody] AllergenCode dto)
        {
            var code = dto.CodePrick?.Trim().ToUpper();
            if (string.IsNullOrEmpty(code))
                return BadRequest("Ο κωδικός αλλεργιογόνου είναι υποχρεωτικός.");
            if (await _context.AllergenCodes.AnyAsync(a => a.CodePrick == code))
                return BadRequest($"Υπάρχει ήδη αλλεργιογόνο με κωδικό '{code}'.");

            var entity = new AllergenCode
            {
                CodePrick = code,
                Company = dto.Company?.Trim().ToUpper(),
                Description = dto.Description?.Trim(),
                DescriptionOther = dto.DescriptionOther?.Trim(),
                DescriptionGreek = dto.DescriptionGreek?.Trim(),
                Category = dto.Category?.Trim(),
                GroupEN = dto.GroupEN?.Trim(),
                GroupGreek = dto.GroupGreek?.Trim(),
                IsActive = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            _context.AllergenCodes.Add(entity);
            await _context.SaveChangesAsync();
            return Ok(entity);
        }

        [HttpPut("allergens/{code}")]
        public async Task<ActionResult<AllergenCode>> UpdateAllergen(string code, [FromBody] AllergenCode dto)
        {
            var entity = await _context.AllergenCodes.FindAsync(code);
            if (entity == null) return NotFound();

            entity.Company = dto.Company?.Trim().ToUpper();
            entity.Description = dto.Description?.Trim();
            entity.DescriptionOther = dto.DescriptionOther?.Trim();
            entity.DescriptionGreek = dto.DescriptionGreek?.Trim();
            entity.Category = dto.Category?.Trim();
            entity.GroupEN = dto.GroupEN?.Trim();
            entity.GroupGreek = dto.GroupGreek?.Trim();
            entity.IsActive = dto.IsActive;
            entity.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            return Ok(entity);
        }
    }
}

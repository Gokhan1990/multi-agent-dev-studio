using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaaSFast.Infrastructure.Data;
using SaaSFast.Domain.Entities;

namespace SaaSFast.Presentation.Controllers
{
    [ApiController]
    [Route("api/ideas")]
    public class IdeasController : ControllerBase
    {
        private readonly AppDbContext _context;
        public IdeasController(AppDbContext context) => _context = context;

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var ideas = await _context.Ideas.ToListAsync();
            return Ok(ideas);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var idea = await _context.Ideas.FindAsync(id);
            return idea == null ? NotFound() : Ok(idea);
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] Idea idea)
        {
            _context.Ideas.Add(idea);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = idea.Id }, idea);
        }
    }
}
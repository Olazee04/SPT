using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPT.Data;
using SPT.Models;

namespace SPT.Controllers
{
    [Authorize(Roles = "Admin")]
    public class CohortController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CohortController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================
        // 1. LIST COHORTS
        // =========================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // Fetch cohorts with a count of students in them
            var cohorts = await _context.Cohorts
                .Include(c => c.Students)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            return View(cohorts);
        }

        // =========================
        // 2. CREATE COHORT
        // =========================
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Cohort model)
        {
            if (ModelState.IsValid)
            {
                // Basic Validation: End date must be after Start date
                if (model.EndDate <= model.StartDate)
                {
                    ModelState.AddModelError("EndDate", "End date must be after start date.");
                    return View(model);
                }

                _context.Cohorts.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Cohort created successfully!";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        // =========================
        // 3. EDIT COHORT
        // =========================
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var cohort = await _context.Cohorts.FindAsync(id);
            if (cohort == null) return NotFound();
            return View(cohort);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Cohort model)
        {
            if (id != model.Id) return NotFound();

            if (ModelState.IsValid)
            {
                if (model.EndDate <= model.StartDate)
                {
                    ModelState.AddModelError("EndDate", "End date must be after start date.");
                    return View(model);
                }

                try
                {
                    _context.Update(model);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cohort updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Cohorts.Any(c => c.Id == id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        // =========================
        // 4. DELETE COHORT
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var cohort = await _context.Cohorts
                .Include(c => c.Students)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (cohort != null)
            {
                // Prevent deleting if students are attached
                if (cohort.Students.Any())
                {
                    TempData["Error"] = $"Cannot delete '{cohort.Name}' because it has {cohort.Students.Count} student(s).";
                }
                else
                {
                    _context.Cohorts.Remove(cohort);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cohort deleted.";
                }
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
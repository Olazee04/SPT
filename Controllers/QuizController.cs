using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SPT.Data;
using SPT.Models;
using SPT.Services;
using System.Reflection;

namespace SPT.Controllers
{
    [Authorize(Roles = "Admin,Mentor")]
    public class QuizController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AuditService _auditService;

        public QuizController(ApplicationDbContext context, AuditService auditService,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
            _auditService = auditService;
        }

        // =========================
        // 1. SELECT MODULE (Step 1)
        // =========================
        [HttpGet]
        public async Task<IActionResult> Index(int? trackId)
        {
            // 1. Get Tracks for Dropdown
            ViewBag.Tracks = await _context.Tracks.ToListAsync();

            // 2. Start Query for Modules
            var query = _context.SyllabusModules
                .Include(m => m.Track)
                .Include(m => m.Questions) // Include questions so we can count them
                .AsQueryable();

            // 3. Filter if needed
            if (trackId.HasValue)
            {
                query = query.Where(m => m.TrackId == trackId.Value);
            }

            // 4. Get List
            var modules = await query
                .OrderBy(m => m.Track.Name)
                .ThenBy(m => m.DisplayOrder)
                .ToListAsync();

            // 5. Send MODULES to the View
            return View(modules);
        }

        // =========================
        // 2. MANAGE QUESTIONS (Step 2)
        // =========================
        [HttpGet]
        public async Task<IActionResult> Manage(int moduleId)
        {
            var module = await _context.SyllabusModules
                .Include(m => m.Track)
                .FirstOrDefaultAsync(m => m.Id == moduleId);

            if (module == null) return NotFound();

            // ✅ Fetch questions and pass via ViewBag
            var questions = await _context.QuizQuestions
                .Include(q => q.Options)
                .Where(q => q.ModuleId == moduleId)
                .OrderBy(q => q.Id)
                .ToListAsync();

            ViewBag.Questions = questions;

            return View(module);
        }

        // =========================
        // 3. CREATE QUESTION (GET)
        // =========================
        [HttpGet]
        public IActionResult CreateQuestion(int moduleId)
        {
            ViewBag.ModuleId = moduleId;

            return View();
        }

        // =========================
        // 4. CREATE QUESTION (POST)
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateQuestion(int moduleId, string questionText, List<string> options, int correctIndex)
        {
            if (string.IsNullOrEmpty(questionText) || options == null || options.Count < 2)
            {
                TempData["Error"] = "Question and at least 2 options are required.";
                return RedirectToAction(nameof(Manage), new { moduleId });
            }

            // Save Question
            var question = new QuizQuestion
            {
                ModuleId = moduleId,
                QuestionText = questionText
            };
            _context.QuizQuestions.Add(question);
            await _context.SaveChangesAsync(); // Save to get ID
            await _auditService.LogAsync(
    "CREATE QUIZ QUESTION",
    $"Question added to ModuleId {moduleId}",
    User.Identity!.Name!,
    HttpContext.Connection.RemoteIpAddress?.ToString()
);

            // Save Options
            for (int i = 0; i < options.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(options[i]))
                {
                    _context.QuizOptions.Add(new QuizOption
                    {
                        QuestionId = question.Id,
                        OptionText = options[i],
                        IsCorrect = (i == correctIndex) // The radio button index determines truth
                    });
                }
            }
            await _context.SaveChangesAsync();

            TempData["Success"] = "Question added successfully!";
            return RedirectToAction(nameof(Manage), new { moduleId });
        }

        // GET: Edit Question
        [HttpGet]
        public async Task<IActionResult> EditQuestion(int id)
        {
            var question = await _context.QuizQuestions
                .Include(q => q.Options)
                .Include(q => q.Module)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question == null) return NotFound();

            ViewBag.ModuleName = question.Module.ModuleName;
            return View(question);
        }

        // POST: Update Question
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditQuestion(int id, QuizQuestion model, List<string> optionTexts, List<bool> isCorrectFlags)
        {
            if (id != model.Id) return NotFound();

            var question = await _context.QuizQuestions
                .Include(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question == null) return NotFound();

            // Update question text
            question.QuestionText = model.QuestionText;

            // Remove old options
            _context.QuizOptions.RemoveRange(question.Options);

            // Add updated options
            for (int i = 0; i < optionTexts.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(optionTexts[i]))
                {
                    question.Options.Add(new QuizOption
                    {
                        OptionText = optionTexts[i].Trim(),
                        IsCorrect = isCorrectFlags.Count > i && isCorrectFlags[i]
                    });
                }
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ Question updated successfully!";
            return RedirectToAction("Manage", new { moduleId = question.ModuleId });
        }

        // =========================
        // 5. DELETE QUESTION
        // =========================
        [HttpPost]
        public async Task<IActionResult> DeleteQuestion(int id)
        {
            var q = await _context.QuizQuestions.FindAsync(id);
            if (q != null)
            {
                int modId = q.ModuleId;
                _context.QuizQuestions.Remove(q);
                await _context.SaveChangesAsync();
                await _auditService.LogAsync(
                "DELETE QUIZ QUESTION",
    $"Question Deleted From ModuleId",
    User.Identity!.Name!,
    HttpContext.Connection.RemoteIpAddress?.ToString()
);
                return RedirectToAction(nameof(Manage), new { moduleId = modId });
            }
            return RedirectToAction(nameof(Index));
        }

        
    }
}
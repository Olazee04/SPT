using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPT.Data;
using SPT.Models;
using SPT.Services;

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
        // 1. MODULE LIST (Mentor-scoped)
        // =========================
        [HttpGet]
        public async Task<IActionResult> Index(int? trackId)
        {
            bool isAdmin = User.IsInRole("Admin");
            int? mentorTrackId = null;
            bool isGeneralMentor = false;

            if (!isAdmin)
            {
                var userId = _userManager.GetUserId(User);
                var mentor = await _context.Mentors.FirstOrDefaultAsync(m => m.UserId == userId);
                if (mentor != null)
                {
                    mentorTrackId = mentor.TrackId;
                    isGeneralMentor = mentor.Specialization == "General" || mentor.TrackId == null;
                }
            }

            // ── Tracks dropdown — mentor only sees their own track unless general ──
            List<Track> availableTracks;
            if (isAdmin || isGeneralMentor)
            {
                availableTracks = await _context.Tracks.ToListAsync();
            }
            else
            {
                availableTracks = await _context.Tracks
                    .Where(t => t.Id == mentorTrackId)
                    .ToListAsync();

                if (!trackId.HasValue && mentorTrackId.HasValue)
                    trackId = mentorTrackId;
            }

            ViewBag.Tracks = availableTracks;
            ViewBag.IsAdmin = isAdmin;
            ViewBag.MentorTrackId = mentorTrackId;

            var query = _context.SyllabusModules
                .Include(m => m.Track)
                .Include(m => m.Questions)   // ✅ Must include so qCount is accurate
                .AsQueryable();

            if (trackId.HasValue)
                query = query.Where(m => m.TrackId == trackId.Value);
            else if (!isAdmin && !isGeneralMentor && mentorTrackId.HasValue)
                query = query.Where(m => m.TrackId == mentorTrackId.Value);

            var modules = await query
                .OrderBy(m => m.Track.Name)
                .ThenBy(m => m.DisplayOrder)
                .ToListAsync();

            // ✅ FIX: Auto-correct any module whose HasQuiz=true but has 0 questions
            bool correctionNeeded = false;
            foreach (var mod in modules)
            {
                if (mod.HasQuiz && (mod.Questions == null || !mod.Questions.Any()))
                {
                    mod.HasQuiz = false;
                    correctionNeeded = true;
                }
            }
            if (correctionNeeded)
                await _context.SaveChangesAsync();

            return View(modules);
        }

        // =========================
        // 2. MANAGE QUESTIONS
        // =========================
        [HttpGet]
        public async Task<IActionResult> Manage(int moduleId)
        {
            var module = await _context.SyllabusModules
                .Include(m => m.Track)
                .FirstOrDefaultAsync(m => m.Id == moduleId);

            if (module == null) return NotFound();

            var questions = await _context.QuizQuestions
                .Include(q => q.Options)
                .Where(q => q.ModuleId == moduleId)
                .OrderBy(q => q.Id)
                .ToListAsync();

            ViewBag.Questions = questions;

            // ✅ FIX: If HasQuiz=true but no questions exist, silently correct it
            if (module.HasQuiz && !questions.Any())
            {
                module.HasQuiz = false;
                await _context.SaveChangesAsync();
            }

            return View(module);
        }

        // =========================
        // 3. UPDATE QUIZ SETTINGS
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateQuizSettings(int moduleId, int passScore)
        {
            // ✅ FIX: Read checkbox from form directly (checkbox only posts when checked)
            bool hasQuiz = Request.Form["hasQuizToggle"] == "on";

            var module = await _context.SyllabusModules.FindAsync(moduleId);
            if (module == null) return NotFound();

            // ✅ FIX: Never allow enabling if no questions
            if (hasQuiz)
            {
                int questionCount = await _context.QuizQuestions
                    .CountAsync(q => q.ModuleId == moduleId);

                if (questionCount == 0)
                {
                    TempData["Error"] = "⚠️ Cannot enable quiz — add at least one question first.";
                    return RedirectToAction(nameof(Manage), new { moduleId });
                }
            }

            module.HasQuiz = hasQuiz;
            module.PassScore = passScore > 0 ? passScore : 75;

            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                "QUIZ_SETTINGS_UPDATED",
                $"Module {module.ModuleCode}: quiz {(hasQuiz ? "ENABLED" : "DISABLED")}, passScore={passScore}",
                User.Identity!.Name!,
                HttpContext.Connection.RemoteIpAddress?.ToString());

            TempData["Success"] = hasQuiz ? "✅ Quiz enabled." : "✅ Quiz disabled.";
            return RedirectToAction(nameof(Manage), new { moduleId });
        }

        // =========================
        // 4. CREATE QUESTION (GET)
        // =========================
        [HttpGet]
        public IActionResult CreateQuestion(int moduleId)
        {
            ViewBag.ModuleId = moduleId;
            return View();
        }

        // =========================
        // 5. CREATE QUESTION (POST)
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateQuestion(int moduleId, string questionText,
            List<string> options, int correctIndex)
        {
            if (string.IsNullOrEmpty(questionText) || options == null || options.Count < 2)
            {
                TempData["Error"] = "Question and at least 2 options are required.";
                return RedirectToAction(nameof(Manage), new { moduleId });
            }

            var question = new QuizQuestion
            {
                ModuleId = moduleId,
                QuestionText = questionText
            };
            _context.QuizQuestions.Add(question);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                "CREATE_QUIZ_QUESTION",
                $"Question added to ModuleId {moduleId}",
                User.Identity!.Name!,
                HttpContext.Connection.RemoteIpAddress?.ToString());

            for (int i = 0; i < options.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(options[i]))
                {
                    _context.QuizOptions.Add(new QuizOption
                    {
                        QuestionId = question.Id,
                        OptionText = options[i],
                        IsCorrect = (i == correctIndex)
                    });
                }
            }
            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ Question added successfully!";
            return RedirectToAction(nameof(Manage), new { moduleId });
        }

        // =========================
        // 6. EDIT QUESTION (GET)
        // =========================
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

        // =========================
        // 7. EDIT QUESTION (POST)
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditQuestion(int id, QuizQuestion model,
            List<string> optionTexts, List<bool> isCorrectFlags)
        {
            if (id != model.Id) return NotFound();

            var question = await _context.QuizQuestions
                .Include(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question == null) return NotFound();

            question.QuestionText = model.QuestionText;
            _context.QuizOptions.RemoveRange(question.Options);

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
        // 8. DELETE QUESTION
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteQuestion(int id)
        {
            var q = await _context.QuizQuestions.FindAsync(id);
            if (q != null)
            {
                int modId = q.ModuleId;
                _context.QuizQuestions.Remove(q);
                await _context.SaveChangesAsync();

                // ✅ Auto-disable if no questions remain
                int remaining = await _context.QuizQuestions.CountAsync(x => x.ModuleId == modId);
                if (remaining == 0)
                {
                    var mod = await _context.SyllabusModules.FindAsync(modId);
                    if (mod != null && mod.HasQuiz)
                    {
                        mod.HasQuiz = false;
                        await _context.SaveChangesAsync();
                        TempData["Warning"] = "⚠️ Last question deleted — quiz auto-disabled.";
                    }
                }
                else
                {
                    TempData["Success"] = "Question deleted.";
                }

                await _auditService.LogAsync(
                    "DELETE_QUIZ_QUESTION",
                    $"Question deleted from ModuleId {modId}",
                    User.Identity!.Name!,
                    HttpContext.Connection.RemoteIpAddress?.ToString());

                return RedirectToAction(nameof(Manage), new { moduleId = modId });
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
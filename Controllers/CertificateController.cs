using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPT.Data;
using SPT.Models;
using SPT.Services;

namespace SPT.Controllers
{
    [Authorize(Roles = "Admin")]
    public class CertificateController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditService _auditService;

        public CertificateController(ApplicationDbContext context, AuditService auditService )
        {
            _context = context;            _auditService = auditService;

        }

        // =========================
        // 1. LIST ISSUED CERTIFICATES
        // =========================
        public async Task<IActionResult> Index()
        {
            var certs = await _context.Certificates
                .Include(c => c.Student)
                .OrderByDescending(c => c.DateIssued)
                .ToListAsync();
            return View(certs);
        }

        // =========================
        // 2. CHECK ELIGIBILITY (Who can graduate?)
        // =========================
        public async Task<IActionResult> Eligibility()
        {
            var students = await _context.Students
                .Include(s => s.Track)
                .Include(s => s.ModuleCompletions)
                .Include(s => s.ProgressLogs)
                .ToListAsync();

            var allModules = await _context.SyllabusModules
                .Where(m => m.IsActive)
                .ToListAsync();

            var eligibleList = students.Select(s =>
            {
                int totalModules = allModules.Count(m => m.TrackId == s.TrackId);
                int completedModules = s.ModuleCompletions.Count(mc => mc.IsCompleted);
                bool isReady = totalModules > 0 && completedModules >= totalModules;

                return new
                {
                    Student = s,
                    CompletedModules = completedModules,   
                    TotalModules = totalModules > 0 ? totalModules : 1, // avoid div by zero
                    TotalHours = s.ProgressLogs.Where(l => l.IsApproved).Sum(l => l.Hours), // keep for reference
                    IsReady = isReady
                };
            }).ToList();

            ViewBag.EligibleList = eligibleList;
            return View();
        }

        // =========================
        // 3. ISSUE CERTIFICATE
        // =========================
        [HttpPost]
        public async Task<IActionResult> Issue(int studentId)
        {
            var student = await _context.Students.Include(s => s.Track).FirstOrDefaultAsync(s => s.Id == studentId);
            if (student == null) return NotFound();

            var cert = new Certificate
            {
                StudentId = student.Id,
                TrackName = student.Track?.Name ?? "Software Engineering",
                DateIssued = DateTime.UtcNow,
                IssuedBy = User.Identity.Name ?? "Admin"
            };

            _context.Certificates.Add(cert);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Certificate issued to {student.FullName}!";
            return RedirectToAction(nameof(Index));
        }

        // =========================
        // 4. VIEW / PRINT CERTIFICATE
        // =========================
        [AllowAnonymous] // Allow public verification if needed
        public async Task<IActionResult> ViewCertificate(string id)
        {
            var cert = await _context.Certificates
                .Include(c => c.Student)
                .FirstOrDefaultAsync(c => c.CertificateId == id || c.Id.ToString() == id);

            if (cert == null) return NotFound();

            return View(cert);
        }
    }
}
using DAL.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Utilities;

namespace PL.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles ="Admin")] // محدش يشوف دي غير الادمن بس
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UsersController(ApplicationDbContext context)
        {
            _context = context;
        }
        public IActionResult Index()
        {
            // عاوز اعرض للادمن كل الناس اللي سجلت ما عدا هو
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
            string userId = claim.Value; // userId دة اللي بيستعرض الصفحة 

            // كلة معادة اللي بيستعرض دلوقتي اللي هو الادمن
            return View(_context.ApplicationUsers.Where(x=>x.Id != userId).ToList()); 
        }

        // المقفول هفتحة و المفتوح هقفلة 
        public IActionResult LockUnlock(string ? id )
        {
            var user = _context.ApplicationUsers.FirstOrDefault(x => x.Id == id);
            if (user == null)
            {
                return NotFound();
            }
            if (user.LockoutEnd == null | user.LockoutEnd < DateTime.Now)
            {
				user.LockoutEnd = DateTime.Now.AddYears(1);
			}
            else
            {
                user.LockoutEnd = DateTime.Now;

			}
            _context.SaveChanges();
            return RedirectToAction(nameof(Index), "Users", new { area = "Admin" });
        }
    }
}

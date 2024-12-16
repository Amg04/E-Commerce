using BLL.Models;
using BLL.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.Operations;
using System.Text.RegularExpressions;

namespace PL.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class CategoryController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;

        public CategoryController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        public IActionResult Index()
        {
            //var categories = _context.Categories.ToList();
            var categories = _unitOfWork.Category.GetAll();

            return View(categories);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken] // db او الحاجات الراجعة لل httpPost بيتحط مع اي ال
                                   // Cross Side Forgery Attacks بيعمل 
        public IActionResult Create(Category category)
        {
			category.Description = StripTags(category.Description);
			// server side Validation
			if (ModelState.IsValid)
            {
                //_context.Categories.Add(category);
                _unitOfWork.Category.Add(category);
                //_context.SaveChanges();
                _unitOfWork.Complete();
                TempData["Create"] = "Data Has Created Successfully";
                return RedirectToAction(nameof(Index));
            }
            //category => هيرجعلة اللي هو كتبه عشان ميرجعشي يكتب من جديد
            return View(category);
        }

        [HttpGet]
        public IActionResult Edit(int? Id)
        {
            if (Id == null || Id == 0)
            {
				return NotFound();
            }
            //var categoryInDb = _context.Categories.Find(Id);
            var categoryInDb = _unitOfWork.Category.Get(x => x.Id == Id);
            return View(categoryInDb);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(Category category)
        {
            if (ModelState.IsValid)
            {
                //_context.Categories.Update(category);
                _unitOfWork.Category.Update(category);
                //_context.SaveChanges();
                _unitOfWork.Complete();
                TempData["Update"] = "Data Has Updated Successfully";
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }


        [HttpGet]
        public IActionResult Delete(int? Id)
        {
            if (Id == null || Id == 0)
            {
				return  NotFound();
            }
            var categoryInDb = _unitOfWork.Category.Get(x => x.Id == Id);
            return View(categoryInDb);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteCategory(int? Id)
        {
            var categoryInDb = _unitOfWork.Category.Get(x => x.Id == Id);
            if (categoryInDb == null)
            {
				return NotFound();
            }
            _unitOfWork.Category.Remove(categoryInDb);
            _unitOfWork.Complete();
            TempData["Delete"] = "Data Has Deleted Successfully";
            return RedirectToAction(nameof(Index));
        }


		public static string StripTags(string input)
		{
			return Regex.Replace(input, "<.*?>", string.Empty);
		}


	}
}

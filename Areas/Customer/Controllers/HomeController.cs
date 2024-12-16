using BLL.Repositories;
using BLL.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Utilities;
using X.PagedList;

namespace PL.Areas.Customer.Controllers
{
	[Area("Customer")]
	public class HomeController : Controller
	{
		private readonly IUnitOfWork _unitOfWork;

		public HomeController(IUnitOfWork unitOfWork)
		{
			_unitOfWork = unitOfWork;
		}
		public IActionResult Index(int? page)
		{
			// عشان احطهم في اكتر من صفحة
			var pageNumber = page ?? 1; // default
			int pageSize = 8;

			var products = _unitOfWork.Product.GetAll().ToPagedList(pageNumber, pageSize);
			return View(products);
		}

		public IActionResult Details(int Id)
		{
			ShoppingCart obj = new ShoppingCart()
			{
				ProductId = Id,
				Product = _unitOfWork.Product.Get(v => v.Id == Id, IncludeWord: "Category"),
				Count = 1// by default
			};
			return View(obj);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		[Authorize]
		public IActionResult Details(ShoppingCart shoppingCart)
		{
			var claimsIdentity = (ClaimsIdentity)User.Identity;
			var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
			shoppingCart.ApplicationUserId = claim.Value;

			// عشان لو موجود اصلا عنصر وانا عاوز ازود علية كمية او احدثة مثلا
			ShoppingCart CartObj = _unitOfWork.ShoppingCart
				// هيجبلي الشوبنج ايدي الحالي اللي عامل لوجن و اللي فية البرودكت الفعلي اللي موجود جوة منة
				// claim.Value => حاليا Authorize الشخص اللي عامل 
				.Get(u => u.ApplicationUserId == claim.Value && u.ProductId == shoppingCart.ProductId);

			if (CartObj == null) // معني كدة انة مش موجود فهضيفة
			{
				_unitOfWork.ShoppingCart.Add(shoppingCart);
				_unitOfWork.Complete();

				HttpContext.Session.SetInt32(SD.SessionKey,
							_unitOfWork.ShoppingCart.GetAll(x => x.ApplicationUserId == claim.Value).ToList().Count());

			}
			else // في حالة انة عدل علي الموجودة
			{
				_unitOfWork.ShoppingCart.IncreaseCount(CartObj, shoppingCart.Count);
				_unitOfWork.Complete();
			}

			return RedirectToAction("Index");
		}
	}
}

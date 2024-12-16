using BLL.Models;
using BLL.Repositories;
using BLL.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Stripe.Checkout;
using System;
using System.Security.Claims;
using Utilities;

namespace PL.Areas.Customer.Controllers
{
	[Area("Customer")]
	[Authorize]
	public class CartController : Controller
	{
		private readonly IUnitOfWork _unitOfWork;
		public ShoppingCartVM ShoppingCartVM { get; set; } // هي نفس اكني عملتها تحت
		public CartController(IUnitOfWork unitOfWork)
		{
			_unitOfWork = unitOfWork;
		}
		public IActionResult Index()
		{
			var claimsIdentity = (ClaimsIdentity)User.Identity;
			var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

			// الكاردس بتاعت اليوزر دة
			ShoppingCartVM = new ShoppingCartVM()
			{
				CartsList = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == claim.Value, IncludeWord: "Product"),
				OrderHeader = new()
			};

			// Total
			foreach (var item in ShoppingCartVM.CartsList)
			{
				ShoppingCartVM.TotalCarts += (item.Count * item.Product.Price);
			}

			return View(ShoppingCartVM);
		}

		public IActionResult Summary()
		{
			var claimsIdentity = (ClaimsIdentity)User.Identity;
			var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

			ShoppingCartVM = new ShoppingCartVM()
			{
				CartsList = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == claim.Value, IncludeWord: "Product"),
				OrderHeader = new()
			};

			ShoppingCartVM.OrderHeader.ApplicationUser = _unitOfWork.ApplicationUser.Get(x => x.Id == claim.Value);


			ShoppingCartVM.OrderHeader.Name = ShoppingCartVM.OrderHeader.ApplicationUser.Name;
			ShoppingCartVM.OrderHeader.Address = ShoppingCartVM.OrderHeader.ApplicationUser.Address;
			ShoppingCartVM.OrderHeader.City = ShoppingCartVM.OrderHeader.ApplicationUser.City;
			ShoppingCartVM.OrderHeader.PhoneNumber = ShoppingCartVM.OrderHeader.ApplicationUser.PhoneNumber;


			foreach (var item in ShoppingCartVM.CartsList)
			{
				ShoppingCartVM.OrderHeader.TotalPrice += (item.Count * item.Product.Price);
			}

			return View(ShoppingCartVM);
		}

		[HttpPost]
		//[ActionName("Summary")]
		[ValidateAntiForgeryToken]
		public IActionResult POSTSummary([FromBody] ShoppingCartVM shoppingCartVM)
		{
			var claimsIdentity = (ClaimsIdentity)User.Identity;
			var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

			shoppingCartVM.CartsList = _unitOfWork.ShoppingCart
				.GetAll(u => u.ApplicationUserId == claim.Value, IncludeWord: "Product");

			var existingOrder = _unitOfWork.OrderHeader
				.Get(u => u.ApplicationUserId == claim.Value && u.PaymentStatus == SD.Pending);



			//var user = _unitOfWork.ApplicationUser.Get(u => u.Id == claim.Value);

			//shoppingCartVM.OrderHeader.Address = user.Address;
			//shoppingCartVM.OrderHeader.Name = user.Name;
			//shoppingCartVM.OrderHeader.City = user.City;
			//shoppingCartVM.OrderHeader.PhoneNumber = user.PhoneNumber;
			//_unitOfWork.OrderHeader.Update(shoppingCartVM.OrderHeader);
			//_unitOfWork.Complete();

			var user = _unitOfWork.ApplicationUser.Get(u => u.Id == claim.Value);

			if (existingOrder != null)
			{
				// تحديث الطلب الحالي
				existingOrder.Address = user.Address;
				existingOrder.City = user.City;
				existingOrder.Name = user.Name;
				existingOrder.PhoneNumber = user.PhoneNumber;
				existingOrder.TotalPrice = 0;
				foreach (var item in shoppingCartVM.CartsList)
				{
					existingOrder.TotalPrice += (item.Count * item.Product.Price);
				}

				existingOrder.OrderDate = DateTime.Now;
				_unitOfWork.OrderHeader.Update(existingOrder);
				_unitOfWork.Complete();

				// حذف تفاصيل الطلب القديمة المرتبطة بالطلب (إن وجدت)
				var oldOrderDetails = _unitOfWork.OrderDetail.GetAll(od => od.OrderHeaderId == existingOrder.Id);
				foreach (var detail in oldOrderDetails)
				{
					_unitOfWork.OrderDetail.Remove(detail);
				}
				_unitOfWork.Complete();

				shoppingCartVM.OrderHeader = existingOrder;
			}
			else
			{
				// إنشاء طلب جديد
				shoppingCartVM.OrderHeader = new OrderHeader
				{
					ApplicationUserId = claim.Value,
					Name = user.Name,
					Address = user.Address,
					City = user.City,
					PhoneNumber = user.PhoneNumber,
					OrderDate = DateTime.Now,
					PaymentStatus = SD.Pending,
					OrderStatus = SD.Pending
				};
				foreach (var item in shoppingCartVM.CartsList)
				{
					shoppingCartVM.OrderHeader.TotalPrice += (item.Count * item.Product.Price);
				}

				_unitOfWork.OrderHeader.Add(shoppingCartVM.OrderHeader);
				_unitOfWork.Complete();
			}


			//shoppingCartVM.OrderHeader.OrderStatus = SD.Pending;
			//shoppingCartVM.OrderHeader.PaymentStatus = SD.Pending;
			//shoppingCartVM.OrderHeader.OrderDate = DateTime.Now;
			//shoppingCartVM.OrderHeader.ApplicationUserId = claim.Value;

			//_unitOfWork.OrderHeader.Add(shoppingCartVM.OrderHeader);
			//_unitOfWork.Complete();

			//_unitOfWork.CompleteOrder(shoppingCartVM.OrderHeader);

			foreach (var item in shoppingCartVM.CartsList)
			{
				OrderDetail orderDetail = new OrderDetail()
				{
					ProductId = item.ProductId,
					OrderHeaderId = shoppingCartVM.OrderHeader.Id,
					Price = item.Product.Price,
					Count = item.Count
				};

				_unitOfWork.OrderDetail.Add(orderDetail);
				_unitOfWork.Complete();

			}
			var domain = "https://localhost:44319/";
			var options = new SessionCreateOptions
			{
				PaymentMethodTypes = new List<string> { "card" },
				LineItems = new List<SessionLineItemOptions>(),

				Mode = "payment",
				SuccessUrl = domain + $"customer/cart/orderconfirmation?id={shoppingCartVM.OrderHeader.Id}",
				CancelUrl = domain + $"customer/cart/index",
			};

			foreach (var item in shoppingCartVM.CartsList)
			{
				var sessionLineOption = new SessionLineItemOptions
				{
					PriceData = new SessionLineItemPriceDataOptions
					{
						UnitAmount = (long)(item.Product.Price * 100),
						Currency = "usd",
						ProductData = new SessionLineItemPriceDataProductDataOptions
						{
							Name = item.Product.Name,
						}
					},
					Quantity = item.Count,
				};
				options.LineItems.Add(sessionLineOption);
			}

			var service = new SessionService();
			Session session = service.Create(options);
			shoppingCartVM.OrderHeader.SessionId = session.Id;

			_unitOfWork.Complete();


			return Json(new { sessionUrl = session.Url });

			//Response.Headers.Add("Location", session.Url);
			//return new StatusCodeResult(303);

			//_unitOfWork.ShoppingCart.RemoveRange(ShoppingCartVM.CartsList);
			//_unitOfWork.Complete();
			//return RedirectToAction("Index", "Home");

		}

		public IActionResult OrderConfirmation(int id)
		{
			OrderHeader orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == id);
			var service = new SessionService();
			Session session = service.Get(orderHeader.SessionId);
			if (session.PaymentStatus.ToLower() == "paid")
			{
				_unitOfWork.OrderHeader.UpdateOrderAndPaymentStatus(id, SD.Approve, SD.Approve);
				orderHeader.PaymentIntentId = session.PaymentIntentId;
				_unitOfWork.Complete();
			}
			List<ShoppingCart> shoppingCarts = _unitOfWork.ShoppingCart
				.GetAll(u => u.ApplicationUserId == orderHeader.ApplicationUserId).ToList();
			_unitOfWork.ShoppingCart.RemoveRange(shoppingCarts);
			_unitOfWork.Complete();
			return View(id);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public IActionResult UpdateFields([FromBody] FieldUpdateVM model)
		{
			if (model == null || model.Updates == null || !model.Updates.Any())
			{
				return BadRequest("Invalid data.");
			}

			var claimsIdentity = (ClaimsIdentity)User.Identity;
			var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

			// جلب المستخدم
			var user = _unitOfWork.ApplicationUser.Get(u => u.Id == claim.Value);

			if (user == null)
			{
				return NotFound("User not found.");
			}

			// تحديث الحقول المطلوبة
			foreach (var update in model.Updates)
			{
				switch (update.Field)
				{
					case "OrderHeader.Name":
						user.Name = update.Value;
						break;
					case "OrderHeader.City":
						user.City = update.Value;
						break;
					case "OrderHeader.PhoneNumber":
						user.PhoneNumber = update.Value;
						break;
					case "OrderHeader.Address":
						user.Address = update.Value;
						break;
					default:
						return BadRequest($"Invalid field: {update.Field}");
				}
			}

			_unitOfWork.ApplicationUser.Update(user);
			_unitOfWork.Complete();

			return Ok("Fields updated successfully.");
		}

		public IActionResult Plus(int? CartId)
		{
			if (CartId == null)
			{
				return NotFound();
			}
			// كدة جبت الكارد اللي عاوز ازودة او انقصة
			var ShoppingCart = _unitOfWork.ShoppingCart
				.Get(x => x.ShoppingCartId == CartId);

			_unitOfWork.ShoppingCart.IncreaseCount(ShoppingCart, 1);
			_unitOfWork.Complete();

			return RedirectToAction(nameof(Index));
		}

		public IActionResult Minus(int? CartId)
		{
			if (CartId == null)
			{
				return NotFound();
			}
			// كدة جبت الكارد اللي عاوز ازودة او انقصة
			var ShoppingCart = _unitOfWork.ShoppingCart
				.Get(x => x.ShoppingCartId == CartId);


			if (ShoppingCart.Count <= 1)
			{
				_unitOfWork.ShoppingCart.Remove(ShoppingCart);

				var count = _unitOfWork.ShoppingCart.
					GetAll(x => x.ApplicationUserId == ShoppingCart.ApplicationUserId).ToList().Count() - 1;

				HttpContext.Session.SetInt32(SD.SessionKey, count);
			}
			else
			{
				_unitOfWork.ShoppingCart.DecreaseCount(ShoppingCart, 1);
			}
			_unitOfWork.Complete();

			return RedirectToAction(nameof(Index));
		}

		public IActionResult Remove(int? CartId)
		{
			var ShoppingCart = _unitOfWork.ShoppingCart.Get(x => x.ShoppingCartId == CartId);
			_unitOfWork.ShoppingCart.Remove(ShoppingCart);
			_unitOfWork.Complete();
			var count = _unitOfWork.ShoppingCart
							 .GetAll(x => x.ApplicationUserId == ShoppingCart.ApplicationUserId)
							 .ToList()
							 .Count();
			HttpContext.Session.SetInt32(SD.SessionKey, count);
			return RedirectToAction(nameof(Index));
		}

	}
}

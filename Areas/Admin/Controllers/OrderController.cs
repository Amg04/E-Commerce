using BLL.Models;
using BLL.Repositories;
using BLL.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Utilities;

namespace PL.Areas.Admin.Controllers
{
	[Area("Admin")]
	[Authorize(Roles = SD.AdminRole)]
	public class OrderController : Controller
	{
		private readonly IUnitOfWork _unitOfWork;
		
		[BindProperty]
        public OrderVM orderVM { get; set; }

        public OrderController(IUnitOfWork unitOfWork)
		{
			_unitOfWork = unitOfWork;
		}
		public IActionResult Index()
		{
			return View();
		}

		public IActionResult GetData()
		{
			IEnumerable<OrderHeader> orderHeader;
			orderHeader = _unitOfWork.OrderHeader.GetAll(IncludeWord: "ApplicationUser");
			return Json(new { data = orderHeader });
		}

		public IActionResult GetDatatwo()
		{
			IEnumerable<OrderHeader> orderHeader;
			orderHeader = _unitOfWork.OrderHeader.GetAll(x => x.OrderStatus == SD.Approve, IncludeWord: "ApplicationUser");
			return Json(new { data = orderHeader });
		}
		public IActionResult ApprovedOrders()
		{
			return View();
		}

	

		public IActionResult Details(int orderid)
		{
			OrderVM orderVM = new OrderVM()
			{
				OrderHeader = _unitOfWork.OrderHeader.Get(o=>o.Id == orderid, IncludeWord: "ApplicationUser"),
				OrderDetails=_unitOfWork.OrderDetail.GetAll(x=>x.OrderHeaderId == orderid, IncludeWord:"Product")
			};
			return View(orderVM);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public IActionResult UpdateOrderDetails(/*int orderid, OrderVM orderVM*/) // بدل ما بعمل كدة بكتبها فوق زي مهي معمولة 
		{
			var orderFromDB = _unitOfWork.OrderHeader.Get(u=>u.Id == orderVM.OrderHeader.Id);
			orderFromDB.Name = orderVM.OrderHeader.Name;
			orderFromDB.PhoneNumber = orderVM.OrderHeader.PhoneNumber;
			orderFromDB.Address = orderVM.OrderHeader.Address;
			orderFromDB.City = orderVM.OrderHeader.City;

			if (orderVM.OrderHeader.Carrier != null)
			{
				orderFromDB.Carrier = orderVM.OrderHeader.Carrier;
			}


			if (orderVM.OrderHeader.TrackingNumber != null)
			{
				orderFromDB.TrackingNumber = orderVM.OrderHeader.TrackingNumber;
			}

			_unitOfWork.OrderHeader.Update(orderFromDB);
			_unitOfWork.Complete();
			TempData["Update"] = "Item has Updated Successfully";

			// new { orderid = orderFromDB.Id } عشان يرجعني علي الدتلز بتاعت الاوردة نفسة 
			return RedirectToAction(nameof(Details), "Order", new { orderid = orderFromDB.Id});
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public IActionResult StartProcess() 
		{
			_unitOfWork.OrderHeader.UpdateOrderAndPaymentStatus(orderVM.OrderHeader.Id, SD.Processing, null);
			_unitOfWork.Complete();

			TempData["Update"] = "Order Status has Updated Successfully";

			return RedirectToAction(nameof(Details), "Order", new { orderid = orderVM.OrderHeader.Id });
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public IActionResult StartShip()
		{
			var orderFromDB = _unitOfWork.OrderHeader.Get(u => u.Id == orderVM.OrderHeader.Id);
			orderFromDB.TrackingNumber = orderVM.OrderHeader.TrackingNumber;
			orderFromDB.Carrier = orderVM.OrderHeader.Carrier;
			orderFromDB.OrderStatus = SD.Shipped;
			orderFromDB.ShippingDate = DateTime.Now;

			_unitOfWork.OrderHeader.Update(orderFromDB);
			_unitOfWork.Complete();

			_unitOfWork.Complete();

			TempData["Update"] = "Order has Shipped Successfully";

			return RedirectToAction(nameof(Details), "Order", new { orderid = orderVM.OrderHeader.Id });
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public IActionResult CancelOrder()
		{
			var orderFromDB = _unitOfWork.OrderHeader.Get(u => u.Id == orderVM.OrderHeader.Id);
			// عشان لو اليوزر دافع ارجعلة فلوسة
			if (orderFromDB.PaymentStatus == SD.Approve)
			{
				var option = new RefundCreateOptions
				{
					Reason = RefundReasons.RequestedByCustomer,
					PaymentIntent = orderFromDB.PaymentIntentId
				};
				var service = new RefundService();
				Refund refund = service.Create(option);

				_unitOfWork.OrderHeader.UpdateOrderAndPaymentStatus(orderFromDB.Id, SD.Cancelled, SD.Refund);
			}
			else
			{
				_unitOfWork.OrderHeader.UpdateOrderAndPaymentStatus(orderFromDB.Id, SD.Cancelled, SD.Cancelled);
			}
			_unitOfWork.Complete();
			TempData["Update"] = "Order  has Cancelled Successfully";

			return RedirectToAction(nameof(Details), "Order", new { orderid = orderVM.OrderHeader.Id });
		}

	}
}

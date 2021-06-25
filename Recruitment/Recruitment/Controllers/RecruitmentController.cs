using Autofac;
using HiStaff.Portal.Client.Controllers;
using HiStaff.Portal.Services;
using HiStaff.Share;
using HiStaff.Share.Code;
using HiStaff.Share.Extensions;
using HiStaff.Share.Model;
using HiStaff.Share.Module;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HiStaff.Portal.Client.Areas.Recruitment.Controllers
{
    [Area("Recruitment")]
    public class RecruitmentController : BaseController
    {
        public RecruitmentController(ILifetimeScope lifetimeScope) : base(lifetimeScope) { }

        #region Quản lý tuyển dụng

        //[Route("employment")]
        public async Task<IActionResult> Employment()
        {
            using var localSerive = _lifetimeScope.BeginLifetimeScope();
            var profileService = localSerive.Resolve<IProfileService>();
            var memoryCacheService = localSerive.Resolve<IMemoryCacheService>();
            try
            {
                var orgtree = await profileService.GetOrgTree(new BaseRequest
                {
                    IsFTEPlan = true,
                    Lang = Langue
                });

                var org = GetTreeview(orgtree.Data, new ParamTreeview
                {
                    ExpandLevel = 5
                });
                return View(new EmploymentResponse
                {
                    OrganizationOptions = org
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex.ToString());
                return BadRequest();
            }
        }

        //[Route("employment-popup")]
        public async Task<IActionResult> EmploymentPopup()
        {
            using var localSerive = _lifetimeScope.BeginLifetimeScope();
            var profileService = localSerive.Resolve<IProfileService>();
            var memoryCacheService = localSerive.Resolve<IMemoryCacheService>();
            try
            {
                var workLocationOpt = await profileService.GetOtherList(new BaseRequest
                {
                    Type = "WORK_LOCATION",
                    IsBlank = true
                });

                return View(new EmploymentPopupModel
                {
                    WorkLocationOptions = workLocationOpt.Data
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex.ToString());
                return BadRequest();
            }
        }

        //[Route("employment/get")]
        public async Task<IActionResult> GetEmployment(string search, decimal? orgId, string type)
        {
            using var localSerive = _lifetimeScope.BeginLifetimeScope();
            logger = localSerive.Resolve<ILogModule>();
            var profileService = localSerive.Resolve<IProfileService>();
            try
            {
                var queryDictionary = QueryHelpers.ParseQuery(HttpContext.Request.QueryString.Value);
                KendoGridFilterModel kendoGridFilter = KendoGridFilterParse.Parse(queryDictionary, "IS_OWNER");
                var filter = kendoGridFilter.GetModel<TitleDTO>();
                if (type == "Left")
                {
                    filter.TEXTBOX_SEARCH = search;
                    filter.ORG_ID_SEARCH = orgId;
                }
                else
                {
                    filter.TEXTBOX2_SEARCH = search;
                    filter.ORG_ID2_SEARCH = orgId;
                    if (orgId == null)
                    {
                        return Json(
                            new
                            {
                                data = new List<TitleDTO>(),
                                total = 0
                            });
                    }
                }
                var userInfo = GetUserInfo();
                var response = await profileService.GetPositionByOrgIDPortal(new GetPositionByOrgIDPortalRequest
                {
                    EmployeeId = userInfo.EMPLOYEE_ID.ToDecimal(0).Value,
                    PageIndex = kendoGridFilter.Page - 1,
                    PageSize = kendoGridFilter.PageSize,
                    Sorts = kendoGridFilter.Sort,
                    _filter = filter
                });

                return Json(
                    new
                    {
                        data = response.Data,
                        total = response.Total
                    });
            }
            catch (Exception ex)
            {
                logger.LogError(ex.ToString());
                return BadRequest();
            }
        }

        [HttpPost]
        //[Route("employment/transfer")]
        public async Task<IActionResult> TransferEmployment(TransferEmploymentModel request)
        {
            using var localSerive = _lifetimeScope.BeginLifetimeScope();
            logger = localSerive.Resolve<ILogModule>();
            var profileService = localSerive.Resolve<IProfileService>();
            var response = new BaseJsonResponse();
            var orgIdRight = 0m;
            try
            {
                if (request == null)
                {
                    return BadRequest();
                }
                if (request.Datas == null || !request.Datas.Any())
                {
                    response.Message = Translate("Model is invalid");
                    return Json(response);
                }
                var check = false;
                var checkLuu = false;
                if (request.OrgIdRight != null)
                {
                    orgIdRight = request.OrgIdRight.Value;
                }
                if ((await profileService.CheckIsOwner(new BaseRequest
                {
                    OrgId = orgIdRight,
                    Lang = Langue
                })).Data)
                {
                    check = true;
                }
                if (orgIdRight < 0)
                {
                    orgIdRight = 0 - orgIdRight;
                }
                foreach (var item in request.Datas)
                {
                    item.IS_PLAN_LEFT = item.IS_PLAN;
                    item.IS_PLAN = orgIdRight < 0;
                    if (item.IS_OWNER.Value && check)
                    {
                        response.Message = Translate("Đơn vị đích đến đã có vị trí trưởng.");
                        return Json(response);
                    }
                    var modifyTitleById = await profileService.ModifyTitleById(new BaseRequest<TitleDTO>
                    {
                        OrgId = orgIdRight,
                        AddressId = item.WORK_LOCATION,
                        Filter = item,
                        Lang = Langue
                    });
                    if (modifyTitleById.Data)
                    {
                        checkLuu = true;
                    }
                }

                response.Status = checkLuu;
                response.Message = Translate(checkLuu ? Constants.CommonMessage.MESSAGE_TRANSACTION_SUCCESS : Constants.CommonMessage.MESSAGE_TRANSACTION_FAIL);
                return Json(response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex.ToString());
                return BadRequest();
            }
        }

        [HttpPost]
        //[Route("employment/clone")]
        public async Task<IActionResult> CloneEmployment(CloneEmploymentModel request)
        {
            using var localSerive = _lifetimeScope.BeginLifetimeScope();
            logger = localSerive.Resolve<ILogModule>();
            var profileService = localSerive.Resolve<IProfileService>();
            var response = new BaseJsonResponse();
            var orgIdRight = 0m;
            try
            {
                if (request == null)
                {
                    return BadRequest();
                }
                if (request.Datas == null || !request.Datas.Any())
                {
                    response.Message = Translate("Model is invalid");
                    return Json(response);
                }
                var check = false;
                var checkLuu = false;
                if (request.OrgIdRight != null)
                {
                    orgIdRight = request.OrgIdRight.Value;
                }
                if ((await profileService.CheckIsOwner(new BaseRequest
                {
                    OrgId = orgIdRight,
                    Lang = Langue
                })).Data)
                {
                    check = true;
                }
                if (orgIdRight < 0)
                {
                    orgIdRight = 0 - orgIdRight;
                }
                foreach (var item in request.Datas)
                {
                    if (item.IS_OWNER.Value && check)
                    {
                        response.Message = Translate("Đơn vị đích đến đã có vị trí trưởng.");
                        return Json(response);
                    }
                    var insertTitleNB = await profileService.InsertTitleNB(new BaseRequest<TitleDTO>
                    {
                        OrgId = orgIdRight,
                        AddressId = item.WORK_LOCATION,
                        Filter = item,
                        Lang = Langue
                    });
                    if (insertTitleNB.Data)
                    {
                        checkLuu = true;
                    }
                }

                response.Status = checkLuu;
                response.Message = Translate(checkLuu ? Constants.CommonMessage.MESSAGE_TRANSACTION_SUCCESS : Constants.CommonMessage.MESSAGE_TRANSACTION_FAIL);
                return Json(response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex.ToString());
                return BadRequest();
            }
        }

        [HttpPost]
        //[Route("employment/clone/get-title-code")]
        public async Task<IActionResult> GetCloneTitleCodeEmployment()
        {
            using var localSerive = _lifetimeScope.BeginLifetimeScope();
            logger = localSerive.Resolve<ILogModule>();
            var profileService = localSerive.Resolve<IProfileService>();
            BaseJsonResponse<decimal> response = new BaseJsonResponse<decimal>();
            try
            {
                var autoGenCodeHuTile = await profileService.AutoGenCodeHuTile(new BaseRequest
                {
                    TableName = "HU_TITLE_ACTIVITIES",
                    ColumnName = "CODE",
                    Lang = Langue
                });
                return Json(new BaseJsonResponse<decimal>
                {
                    Status = true,
                    Data = autoGenCodeHuTile.Data.ToDecimal(0).Value
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex.ToString());
                return BadRequest();
            }
        }

        #endregion

        //[Route("employment-location")]
        public IActionResult EmploymentLocationPopup()
        {
            return View();
        }
        //[Route("employment-clone")]
        public IActionResult EmploymentClonePopup()
        {
            return View();
        }
    }
}

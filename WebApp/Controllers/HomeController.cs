using System;
using System.Web;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Google.Apis.Auth.AspNetCore3;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Classroom.v1;
using Google.Apis.Classroom.v1.Data;
using Google.Apis.Services;
using Microsoft.AspNetCore.Authorization;
using Google.Apis.Drive.v3;
using Microsoft.AspNetCore.Http;
using System.IO;
using MimeMapping;
using static Google.Apis.Requests.BatchRequest;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics;
namespace iscl.gclassforteams.Web.Controllers
{
    public class CourseData
    {
        public IList<Course> courses;
        public Dictionary<string, IList<CourseWorkUser>> courseData;
        public ClassroomService classService;
        public DriveService driveService;
        public Dictionary<string, IList<Teacher>> teachers;
        public UserProfile me;
        public DisplayResult displayResult = null;
    }

    public class SubmissionData
    {
        public IList<StudentSubmissionUser> submissions;
        public Course course;
        public CourseWorkUser courseWork;
    }

    public class GeneralOperations
    {
        public static String addzeros(int number, int chars)
        {
            String numberString = number.ToString();
            for (int i = numberString.Length; i < chars; i++)
            {
                numberString = "0" + numberString;
            }
            return numberString;
        }
        
        public static List<String> parseByDelim(string input, string delim)
        {
            int LastPos = 0;
            List<String> items = new List<string>();
            for(int i = 0; i < input.Count() - delim.Count(); i++)
            {
                if(input.Substring(i, delim.Count()) == delim){
                    items.Add(input.Substring(LastPos, i - LastPos));
                    LastPos = i + delim.Count();
                    i = LastPos;
                }
            }
            return items;
        }
    }

    public class ClassroomOperations
    {
        public static Dictionary<String, UserProfile> userData = new Dictionary<string, UserProfile>();
        public List<CourseWork> getClassWork(string courseId, ref ClassroomService service)
        {
            string pageToken = null;
            var classWorkList = new List<CourseWork>();
            do
            {
                var request = service.Courses.CourseWork.List(courseId);
                request.PageSize = 100;
                request.PageToken = pageToken;
                var response = request.Execute();
                if (response.CourseWork != null)
                {
                    classWorkList.AddRange(response.CourseWork);
                }
                pageToken = response.NextPageToken;
            } while (pageToken != null);
            return classWorkList;
        }

        public IList<StudentSubmissionUser> compareSubmission(IList<StudentSubmissionUser> currentList, StudentSubmissionUser newAdd)
        {
            bool exists = false;
            for(int i = 0; i < currentList.Count; i++)
            {
                if(currentList[i].UserId == newAdd.UserId)
                {
                    exists = true;
                    if (DateTime.Parse(currentList[i].UpdateTime.ToString()) < DateTime.Parse(newAdd.UpdateTime.ToString()))
                    {
                        currentList[i] = newAdd;
                        break;
                    }
                }
            }
            if (!exists)
            {
                currentList.Add(newAdd);
            }
            return currentList;
        }

        public IList<StudentSubmissionUser> sortByName(IList<StudentSubmissionUser> list)
        {
            var newList = list.OrderByDescending(o => o.name).ToList();
            return newList;
        }

        public SubmissionData getSubmissionData(string courseId, string workId, CourseData cd, ref ClassroomService service)
        {
            var courseworks = cd.courseData[courseId];
            CourseWorkUser coursework = null;
            Course course = null;
            foreach(Course c in cd.courses)
            {
                if(c.Id == courseId)
                {
                    course = c;
                    break;
                }
            }
            foreach(CourseWorkUser cwu in courseworks)
            {
                if(cwu.Id == workId)
                {
                    coursework = cwu;
                    break;
                }
            }
            IList<StudentSubmission> studentSubmissions = coursework.studentSubmissions;
            IList<StudentSubmissionUser> newSubmissions = new List<StudentSubmissionUser>();
            foreach (StudentSubmission ss in studentSubmissions)
            {
                StudentSubmissionUser ssu = toStudentSubmissionUser(ss);
                ssu.name = getUser(ss.UserId, ref service).Name.FullName;
                newSubmissions = compareSubmission(newSubmissions, ssu);
            }
            newSubmissions = sortByName(newSubmissions);
            SubmissionData subData = new SubmissionData();
            subData.course = course;
            subData.courseWork = coursework;
            subData.submissions = newSubmissions;
            return subData;
        }

        public IList<Course> getCourses(ref ClassroomService service)
        {
            CoursesResource.ListRequest request = service.Courses.List();
            IList<Course> courses = request.Execute().Courses;
            return courses;
        }

        public List<CourseWorkMaterial> getCourseMaterials(string courseId, ref ClassroomService service)
        {
            string pageToken = null;
            var classWorkList = new List<CourseWorkMaterial>();
            do
            {
                var request = service.Courses.CourseWorkMaterials.List(courseId);
                request.PageSize = 100;
                request.PageToken = pageToken;
                var response = request.Execute();
                if (response.CourseWorkMaterial != null)
                {
                    classWorkList.AddRange(response.CourseWorkMaterial);
                }
                pageToken = response.NextPageToken;
            } while (pageToken != null);
            return classWorkList;
        }

        public void separateCourses(ref IList<Course> activeCourses, ref IList<Course> inactiveCourses, IList<Course> allCourses)
        {
            foreach (Course c in allCourses)
            {
                if (c.CourseState == "ACTIVE")
                {
                    activeCourses.Add(c);
                }
                else
                {
                    inactiveCourses.Add(c);
                }
            }
        }

        public CourseWork toCourseWork(CourseWorkMaterial cm)
        {
            CourseWork cw = new CourseWork();
            cw.AlternateLink = cm.AlternateLink;
            cw.AssigneeMode = cm.AssigneeMode;
            cw.CourseId = cm.CourseId;
            cw.CreationTime = cm.CreationTime;
            cw.CreatorUserId = cm.CreatorUserId;
            cw.Description = cm.Description;
            cw.ETag = cm.ETag;
            cw.Id = cm.Id;
            cw.IndividualStudentsOptions = cm.IndividualStudentsOptions;
            cw.Materials = cm.Materials;
            cw.ScheduledTime = cm.ScheduledTime;
            cw.State = cm.State;
            cw.Title = cm.Title;
            cw.TopicId = cm.TopicId;
            cw.UpdateTime = cm.UpdateTime;
            return cw;
        }

        public StudentSubmissionUser toStudentSubmissionUser(StudentSubmission insub)
        {
            StudentSubmissionUser outsub = new StudentSubmissionUser();
            outsub.AlternateLink = insub.AlternateLink;
            outsub.AssignedGrade = insub.AssignedGrade;
            outsub.AssignmentSubmission = insub.AssignmentSubmission;
            outsub.AssociatedWithDeveloper = insub.AssociatedWithDeveloper;
            outsub.CourseId = insub.CourseId;
            outsub.CourseWorkId = insub.CourseWorkId;
            outsub.CourseWorkType = insub.CourseWorkType;
            outsub.CreationTime = insub.CreationTime;
            outsub.DraftGrade = insub.DraftGrade;
            outsub.ETag = insub.ETag;
            outsub.Id = insub.Id;
            outsub.Late = insub.Late;
            outsub.MultipleChoiceSubmission = insub.MultipleChoiceSubmission;
            outsub.ShortAnswerSubmission = insub.ShortAnswerSubmission;
            outsub.State = insub.State;
            outsub.SubmissionHistory = insub.SubmissionHistory;
            outsub.UpdateTime = insub.UpdateTime;
            outsub.UserId = insub.UserId;
            return outsub;
        }

        public StudentSubmission toStudentSubmission(StudentSubmissionUser insub)
        {
            StudentSubmission outsub = new StudentSubmission();
            outsub.AlternateLink = insub.AlternateLink;
            outsub.AssignedGrade = insub.AssignedGrade;
            outsub.AssignmentSubmission = insub.AssignmentSubmission;
            outsub.AssociatedWithDeveloper = insub.AssociatedWithDeveloper;
            outsub.CourseId = insub.CourseId;
            outsub.CourseWorkId = insub.CourseWorkId;
            outsub.CourseWorkType = insub.CourseWorkType;
            outsub.CreationTime = insub.CreationTime;
            outsub.DraftGrade = insub.DraftGrade;
            outsub.ETag = insub.ETag;
            outsub.Id = insub.Id;
            outsub.Late = insub.Late;
            outsub.MultipleChoiceSubmission = insub.MultipleChoiceSubmission;
            outsub.ShortAnswerSubmission = insub.ShortAnswerSubmission;
            outsub.State = insub.State;
            outsub.SubmissionHistory = insub.SubmissionHistory;
            outsub.UpdateTime = insub.UpdateTime;
            outsub.UserId = insub.UserId;
            return outsub;
        }

        public UserProfile getUser(string userId, ref ClassroomService service)
        {
            if (userData.ContainsKey(userId))
            {
                return userData[userId];
            }
            else
            {
                var request = service.UserProfiles.Get(userId);
                var response = request.Execute();
                userData[userId] = response;
                return response;
            }
        }

        public IList<CourseWork> sortCourseWork(IList<CourseWork> cw)
        {
            SortedDictionary<String, int> dateSort = new SortedDictionary<string, int>();
            List<CourseWork> sorted = new List<CourseWork>();
            int count = 0;
            foreach (CourseWork work in cw)
            {
                DateTime date = DateTime.Parse(work.CreationTime.ToString());
                String forSortDate = GeneralOperations.addzeros(date.Year, 4) +
                    "/" + GeneralOperations.addzeros(date.Month, 2) +
                    "/" + GeneralOperations.addzeros(date.Day, 2) +
                    "/" + GeneralOperations.addzeros(date.Hour, 2) +
                    "/" + GeneralOperations.addzeros(date.Minute, 2) +
                    "/" + GeneralOperations.addzeros(date.Second, 2);
                dateSort[forSortDate] = count;
                count++;
            }
            foreach (String date in dateSort.Keys)
            {
                sorted.Add(cw[dateSort[date]]);
            }
            sorted.Reverse();
            return sorted;
        }

        public IList<CourseWork> orderCourseMaterials(IList<CourseWork> cw, IList<CourseWorkMaterial> cm)
        {
            IList<CourseWork> allMaterials = new List<CourseWork>();
            foreach (CourseWork work in cw)
            {
                allMaterials.Add(work);
            }
            foreach (CourseWorkMaterial material in cm)
            {
                allMaterials.Add(toCourseWork(material));
            }
            return sortCourseWork(allMaterials);
        }

        public CourseWork toCourseWork(CourseWorkUser cwu)
        {
            CourseWork cw = new CourseWork();
            cw.AlternateLink = cwu.AlternateLink;
            cw.AssigneeMode = cwu.AssigneeMode;
            cw.Assignment = cwu.Assignment;
            cw.AssociatedWithDeveloper = cwu.AssociatedWithDeveloper;
            cw.CourseId = cwu.CourseId;
            cw.CreationTime = cwu.CreationTime;
            cw.CreatorUserId = cwu.CreatorUserId;
            cw.Description = cwu.Description;
            cw.DueDate = cwu.DueDate;
            cw.DueTime = cwu.DueTime;
            cw.ETag = cwu.ETag;
            cw.Id = cwu.Id;
            cw.IndividualStudentsOptions = cwu.IndividualStudentsOptions;
            cw.Materials = cwu.Materials;
            cw.MaxPoints = cwu.MaxPoints;
            cw.MultipleChoiceQuestion = cwu.MultipleChoiceQuestion;
            cw.ScheduledTime = cwu.ScheduledTime;
            cw.State = cwu.State;
            cw.SubmissionModificationMode = cwu.SubmissionModificationMode;
            cw.Title = cwu.Title;
            cw.TopicId = cwu.TopicId;
            cw.UpdateTime = cwu.UpdateTime;
            cw.WorkType = cwu.WorkType;
            return cw;
        }

        public CourseWorkUser toCourseWorkUser(CourseWork cw)
        {
            CourseWorkUser cwu = new CourseWorkUser();
            cwu.AlternateLink = cw.AlternateLink;
            cwu.AssigneeMode = cw.AssigneeMode;
            cwu.Assignment = cw.Assignment;
            cwu.AssociatedWithDeveloper = cw.AssociatedWithDeveloper;
            cwu.CourseId = cw.CourseId;
            cwu.CreationTime = cw.CreationTime;
            cwu.CreatorUserId = cw.CreatorUserId;
            cwu.Description = cw.Description;
            cwu.DueDate = cw.DueDate;
            cwu.DueTime = cw.DueTime;
            cwu.ETag = cw.ETag;
            cwu.Id = cw.Id;
            cwu.IndividualStudentsOptions = cw.IndividualStudentsOptions;
            cwu.Materials = cw.Materials;
            cwu.MaxPoints = cw.MaxPoints;
            cwu.MultipleChoiceQuestion = cw.MultipleChoiceQuestion;
            cwu.ScheduledTime = cw.ScheduledTime;
            cwu.State = cw.State;
            cwu.SubmissionModificationMode = cw.SubmissionModificationMode;
            cwu.Title = cw.Title;
            cwu.TopicId = cw.TopicId;
            cwu.UpdateTime = cw.UpdateTime;
            cwu.WorkType = cw.WorkType;
            return cwu;
        }


        public static List<Material> getDriveMaterials(List<DriveFile> driveFiles)
        {
            List<Material> materials = new List<Material>();
            foreach (DriveFile driveFile in driveFiles)
            {
                Material m = new Material();
                SharedDriveFile sdf = new SharedDriveFile();
                sdf.DriveFile = driveFile;
                sdf.ShareMode = "VIEW";
                m.DriveFile = sdf;
                materials.Add(m);
            }
            return materials;
        }
    }


    public class DriveOperations
    {
        private static String SaveUploadedFile(IFormFile file)
        {
            String path = null;
            if (file != null)
            {
                var fileName = file.FileName;
                path = "C:\\Users\\SSAPP\\Documents\\code\\c#\\TeamsClassroomLinker\\WebApp\\" + fileName;
                using (var stream = System.IO.File.Create(path))
                {
                    file.CopyTo(stream);
                }
            }
            return path;
        }

        public static DriveFile DriveUpload(ref DriveService ds, string path)
        {
            var FileMetaData = new Google.Apis.Drive.v3.Data.File();
            FileMetaData.Name = Path.GetFileName(path);
            FileMetaData.MimeType = MimeMapping.MimeUtility.GetMimeMapping(path);
            DriveFile d = new DriveFile();
            using (var stream = new System.IO.FileStream(path, System.IO.FileMode.Open))
            {
                var request = ds.Files.Create(FileMetaData, stream, FileMetaData.MimeType);
                request.Fields = "id";
                var response = request.Upload();
                d.Id = request.ResponseBody.Id;
                d.ETag = request.ResponseBody.ETag;
                d.AlternateLink = request.ResponseBody.WebViewLink;
                d.ThumbnailUrl = request.ResponseBody.ThumbnailLink;
                d.Title = request.ResponseBody.Name;
            }
            return d;
          
        }
        public static DriveFile FileUpload(ref DriveService ds, IFormFile file)
        {
            String path = SaveUploadedFile(file);
            if (path != null)
            {
                return DriveUpload(ref ds, path);
            }
            return null;
        }
        public static List<DriveFile> FilesUpload(ref DriveService ds, List<IFormFile> files)
        {
            DriveFile currentDriveFile;
            List<DriveFile> driveIds = new List<DriveFile>();
            foreach(IFormFile file in files)
            {
                currentDriveFile = FileUpload(ref ds, file);
                if(currentDriveFile != null)
                {
                    driveIds.Add(currentDriveFile);
                }
            }
            return driveIds;
        }
        public static DriveFolder createDriveFolder(ref DriveService ds, string name)
        {
            var FileMetaData = new Google.Apis.Drive.v3.Data.File();
            FileMetaData.Name = name;
            FileMetaData.MimeType = "application/vnd.google-apps.folder";
            var request = ds.Files.Create(FileMetaData);
            request.Fields = "id";
            var response = request.Execute();
            DriveFolder df = new DriveFolder();
            df.AlternateLink = response.WebViewLink;
            df.ETag = response.ETag;
            df.Id = response.Id;
            df.Title = response.Name;
            return df;
        }
    }



    public class CourseWorkUser : CourseWork
    {
        public String creator;
        public IList<StudentSubmission> studentSubmissions;
    }

    public class StudentSubmissionUser : StudentSubmission
    {
        public String name;
    }

    public class DisplayResult
    {
        public string redirectUrl = null;
        public string alertMessage = null;
    }

    public class HomeController : Controller
    {
        public static CourseData courseDataPublic;
        public static ClassroomOperations classroomOperationsPublic;
        [Authorize]
        [Route("")]
        [GoogleScopedAuthorize(ClassroomService.ScopeConstants.ClassroomCourseworkmaterials,
            ClassroomService.ScopeConstants.ClassroomCourseworkStudents,
            ClassroomService.ScopeConstants.ClassroomCourseworkMe,
            ClassroomService.ScopeConstants.ClassroomRosters,
            ClassroomService.ScopeConstants.ClassroomCourses,
            DriveService.ScopeConstants.Drive)]
        public async Task<IActionResult> Index([FromServices] IGoogleAuthProvider auth)
        {
            GoogleCredential cred = await auth.GetCredentialAsync();
            var classService = new ClassroomService(new BaseClientService.Initializer
            {
                HttpClientInitializer = cred
            });
            var driveService = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = cred
            });
            CourseData cd = new CourseData();
            ClassroomOperations cf = new ClassroomOperations();
            IList<Course> courses = cf.getCourses(ref classService);
            IList<Course> activeCourses = new List<Course>();
            IList<Course> inactiveCourses = new List<Course>();
            cf.separateCourses(ref activeCourses, ref inactiveCourses, courses);
            cd.courses = activeCourses;
            Dictionary<string, IList<CourseWork>> courseData = new Dictionary<string, IList<CourseWork>>();
            Dictionary<string, IList<CourseWorkUser>> courseDataNew = new Dictionary<string, IList<CourseWorkUser>>();
            Dictionary<string, Dictionary<string, IList<StudentSubmission>>> submissionData = new Dictionary<string, Dictionary<string, IList<StudentSubmission>>>();
            Dictionary<string, IList<Teacher>> teacherData = new Dictionary<string, IList<Teacher>>();
            CoursesResource.CourseWorkResource.StudentSubmissionsResource.ListRequest submissionsRequest;
            ListStudentSubmissionsResponse submissionsResponse;
            foreach (Course course in activeCourses)
            {
                submissionData[course.Id] = new Dictionary<string, IList<StudentSubmission>>();
                submissionsRequest = classService.Courses.CourseWork.StudentSubmissions.List(course.Id, "-");
                submissionsResponse = submissionsRequest.Execute();
                if (submissionsResponse.StudentSubmissions != null)
                {
                    foreach (StudentSubmission studsub in submissionsResponse.StudentSubmissions.ToList<StudentSubmission>())
                    {
                        if (!submissionData[course.Id].ContainsKey(studsub.CourseWorkId))
                        {
                            submissionData[course.Id][studsub.CourseWorkId] = new List<StudentSubmission>();
                        }
                        submissionData[course.Id][studsub.CourseWorkId].Add(studsub);
                        submissionData[course.Id][submissionsResponse.StudentSubmissions[submissionsResponse.StudentSubmissions.Count - 1].CourseWorkId] = submissionsResponse.StudentSubmissions;
                    }
                }
                else
                {
                    submissionData[course.Id] = new Dictionary<string, IList<StudentSubmission>>();
                }
                
                courseData[course.Id] = cf.orderCourseMaterials(cf.getClassWork(course.Id, ref classService),
                                                                            cf.getCourseMaterials(course.Id, ref classService));
            }
            foreach(Course c in activeCourses)
                {
                    IList<CourseWorkUser> newCourseData = new List<CourseWorkUser>();
                foreach (CourseWork cw in courseData[c.Id])
                {
                    
                    CourseWorkUser cwu = cf.toCourseWorkUser(cw);
                    if (submissionData[c.Id].ContainsKey(cw.Id)) 
                    { 
                        cwu.studentSubmissions = submissionData[c.Id][cw.Id];
                    }
                    cwu.creator = cf.getUser(cw.CreatorUserId, ref classService).Name.FullName;
                    newCourseData.Add(cwu);
                }
                    courseDataNew[c.Id] = newCourseData;
                    var teacherrequest = classService.Courses.Teachers.List(c.Id);
                    var teacherresponse = teacherrequest.Execute();
                    teacherData[c.Id] = teacherresponse.Teachers;
                }
                var merequest = classService.UserProfiles.Get("me");
                var meresponse = merequest.Execute();
                cd.teachers = teacherData;
                cd.courseData = courseDataNew;
                cd.classService = classService;
                cd.driveService = driveService;
                cd.me = meresponse;
                cd.displayResult = null;
                courseDataPublic = cd;
                classroomOperationsPublic = cf;
            return View(cd);
        }

        [HttpPost]
        [Route ("HomeController/Upload")]
        public ActionResult Upload(IFormFile file, String link)
        {
            DriveOperations.FileUpload(ref courseDataPublic.driveService, file);
            courseDataPublic.displayResult = new DisplayResult
            {
                alertMessage = "Your file has been uploaded, you will now be redirected to the class to add the work. Click 'Add or Create', then file and then go to the recents tab. The file you just uploaded is the top of the list.",
                redirectUrl = link
            };
            return View("Index", courseDataPublic);
        }

        [HttpPost]
        [Route("HomeController/ChangeCourse")]
        public ActionResult ChangeCourse(string updateField, string newValue, string courseId)
        {
            for(int i = 0; i < courseDataPublic.courses.Count; i++)
            {
                if (courseDataPublic.courses[i].Id == courseId)
                {
                    Course c = courseDataPublic.courses[i];
                    if (updateField == "name")
                    {
                        c.Name = newValue;
                    }
                    else if (updateField == "description")
                    {
                        c.Description = newValue;
                    }
                    else if (updateField == "descriptionHeading")
                    {
                        c.DescriptionHeading = newValue;
                    }
                    CoursesResource.PatchRequest pr = courseDataPublic.classService.Courses.Patch(c, courseId);
                    pr.UpdateMask = updateField;
                    courseDataPublic.courses[i] = pr.Execute();
                    break;
                }
            }
            return View("Index", courseDataPublic);
        }


        [HttpPost]
        [Route ("HomeController/CreateCourse")]
        public ActionResult CreateCourse(String Name, String DescriptionHeading,
                                         String Description)
        {
            var course = new Course
            {
                Name = Name,
                DescriptionHeading = DescriptionHeading,
                Description = Description,
                OwnerId = courseDataPublic.me.Id,
                CourseState = "ACTIVE"
            };
            try
            {
                course = courseDataPublic.classService.Courses.Create(course).Execute();
                courseDataPublic.displayResult = new DisplayResult { alertMessage = "Course created sucessfully" };
            }
            catch { 
                course.CourseState = "PROVISIONED";
                course = courseDataPublic.classService.Courses.Create(course).Execute();
                courseDataPublic.displayResult = new DisplayResult { alertMessage = "Course created successfully. Now finish the creation process by accepting ownership on the next page.", redirectUrl = course.AlternateLink };
            }
            return View("Index", courseDataPublic);
        }

        [HttpPost]
        [Route ("HomeController/CreateCourseWork")]
        public ActionResult CreateCourseWork(String Title, String Description, List<IFormFile> Materials, 
                                             String WorkType, String State, String courseId, String DateDue, String TimeDue,
                                             String points, String scheduledDay, String scheduledTime, String driveFolder,
                                             String multipleChoiceOptions)
        {

            CourseWork cw = new CourseWork();

            if (Materials.Count > 0)
            {
                cw.Materials = ClassroomOperations.getDriveMaterials(DriveOperations.FilesUpload(ref courseDataPublic.driveService, Materials));
            }

            cw.Title = Title;
            cw.Description = Description;
            cw.WorkType = WorkType;
            cw.State = State;
            if (DateDue != null){
                Date DueDate = new Date();
                TimeOfDay DueTime = new TimeOfDay();
                DateTime parsedDate = DateTime.Parse(DateDue);
                DueDate.Day = parsedDate.Day;
                DueDate.Month = parsedDate.Month;
                DueDate.Year = parsedDate.Year;
                cw.DueDate = DueDate;
                if(TimeDue != null)
                {
                    DueTime.Hours = int.Parse(TimeDue.Substring(0, 2));
                    DueTime.Minutes = int.Parse(TimeDue.Substring(3, 2));
                }
                else
                {
                    DueTime.Hours = 0;
                    DueTime.Minutes = 0;
                }
                cw.DueTime = DueTime;
            }
            if (points != null) {
                cw.MaxPoints = double.Parse(points);
            }
            if (scheduledTime != null) {
                DateTime d = DateTime.Parse(scheduledDay + " " + scheduledTime);
                scheduledTime = $"{d.Year}-{d.Month}-{d.Year}T{d.Hour}:{d.Minute}:{d.Second}.{d.Millisecond}Z";
                cw.ScheduledTime = scheduledTime;
            }
            cw.CourseId = courseId;
            if (WorkType == "ASSIGNMENT") {
                Assignment a = new Assignment();
                a.StudentWorkFolder = DriveOperations.createDriveFolder(ref courseDataPublic.driveService, driveFolder);
                cw.Assignment = a;
            }
            if (WorkType == "MULTIPLE_CHOICE_QUESTION") {
                MultipleChoiceQuestion mcq = new MultipleChoiceQuestion();
                mcq.Choices = GeneralOperations.parseByDelim(multipleChoiceOptions, ",,,");
                cw.MultipleChoiceQuestion = mcq;
            }
            var request = courseDataPublic.classService.Courses.CourseWork.Create(cw, courseId);
            var response = request.Execute();
            response.CreatorUserId = courseDataPublic.me.Id;
            ClassroomOperations co = new ClassroomOperations();
            var work = co.toCourseWorkUser(response);
            work.creator = courseDataPublic.me.Name.FullName;
            courseDataPublic.courseData[courseId].Insert(0, work);
            return View("Index", courseDataPublic);
        }

        [HttpPost]
        [Route("HomeController/ViewSubmissions")]
        public ActionResult ViewSubmissions(String courseId, String workId)
        {
           var submissionData = classroomOperationsPublic.getSubmissionData(courseId, workId, courseDataPublic, ref courseDataPublic.classService);
            return View("SubmissionView", submissionData);
        }
        
        [HttpPost]
        [Route("HomeController/CloseSubmissions")]
        public ActionResult CloseSubmissions()
        {
            return View("Index", courseDataPublic);
        }

        [Route("hello")]
        public ActionResult Hello()
        {
            return View("Index");
        }

        [Route("first")]
        public ActionResult First()
        {
            return View();
        }

        [Route("second")]
        public ActionResult Second()
        {
            return View();
        }

        [Route("configure")]
        public ActionResult Configure()
        {
            return View();
        }




    }
}
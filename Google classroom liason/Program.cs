using Google.Apis.Auth.OAuth2;
using Google.Apis.Classroom.v1;
using Google.Apis.Classroom.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace ClassroomQuickstart
{
    class Program
    {
        static string[] Scopes = { ClassroomService.Scope.ClassroomCoursesReadonly,
                                    ClassroomService.Scope.ClassroomCourseworkmaterials,
                                    ClassroomService.Scope.ClassroomCourseworkMe};
        static string ApplicationName = "Teams-Classroom Bridge";

        public static List<CourseWork> getClassWork(string courseId, ref ClassroomService service)
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

        public static IList<Course> getCourses(ref ClassroomService service)
        {
            CoursesResource.ListRequest request = service.Courses.List();
            IList<Course> courses = request.Execute().Courses;
            return courses;
        }

        public static List<CourseWorkMaterial> getCourseMaterials(string courseId, ref ClassroomService service)
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

        public static ClassroomService getCreds()
        {
            UserCredential credential;
            
            

            string credPath = "token.json";

            credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                new ClientSecrets
                {
                    ClientId = "1087779108035-kddarlprc6u7bfp94nfvm9f9v0l4qgci.apps.googleusercontent.com",
                    ClientSecret = "JdB7oZrz-SLtQlt6Sv_-rmF1"
                },
                Scopes,
                "user",
                CancellationToken.None,
                new FileDataStore(credPath, true)).Result;
            var service = new ClassroomService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });
            return service;
        }

        static void separateCourses(ref IList<Course> activeCourses, ref IList<Course> inactiveCourses, IList<Course> allCourses)
        {
            foreach(Course c in allCourses)
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

        static void Main(string[] args)
        {

            ClassroomService service = getCreds();
            IList<Course> courses = getCourses(ref service);
            IList<Course> activeCourses = new List<Course>() ;
            IList<Course> inactiveCourses = new List<Course>();
            separateCourses(ref activeCourses, ref inactiveCourses, courses);
            Dictionary<string, IList<CourseWork>> courseData = new Dictionary<string, IList<CourseWork>>();
            foreach (Course course in activeCourses)
            {
                courseData[course.Id] = getClassWork(course.Id, ref service);
            }
        }
    }
}


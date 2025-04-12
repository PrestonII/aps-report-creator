using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ipx.revit.reports.Models;

namespace ipx.revit.reports.Services
{
    public static class ProjectDataValidationService
    {
        private static JsonValidationService _service = new();

        static ProjectDataValidationService ()
        {
            _service = new();
        }

        public static ProjectData ValidateProjectData()
        {
            // Validate and parse the input JSON file
            ProjectData projectData = _service.ValidateAndParseProjectData("params.json");

            // set the service to use the right enviroment if it's not debug
            if(projectData.Environment != null && projectData.Environment != "debug")
            {
                _service = new(projectData.Environment);
            }
            
            return projectData;
        }
    }
}

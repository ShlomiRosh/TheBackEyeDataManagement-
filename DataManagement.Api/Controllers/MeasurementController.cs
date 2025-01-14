﻿using DbAccess.RepositoryInterfaces;
using Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace DataManagement.Api.Controllers
{
    /// <summary>
    /// MeasurementController is responsible for all the Measurement's CRUD operations using API calls 
    /// </summary>
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class MeasurementController : ControllerBase
    {
        private readonly ILogger _logger;
        private readonly IMeasurementRepository _measurementRepository;
        private readonly MeasurementsHub _measurementsHub;

        public MeasurementController(ILogger<MeasurementController> logger, IMeasurementRepository measurementRepository, IHubContext<MeasurementsHub> hub, MeasurementsHub measurementsHub)
        {
            _logger = logger;
            _measurementRepository = measurementRepository;
            _measurementsHub = measurementsHub;
        }

        /// <summary>
        /// This API is just for testing - it sends new measurements to connected clients
        /// </summary>
        /// <returns>Ok if success, expception string otherwise</returns>
        [HttpGet("TestSignalR")]
        [AllowAnonymous]
        public async Task<ActionResult<MeasurementDto[]>> TestSignalR()
        {
            var m1 = new MeasurementDto()
            {
                DateTime = DateTime.Now,
                LessonId = 1,
                PersonId = 17,
                Id = 1,
                FaceRecognition = true,
                SleepDetector = true,
                OnTop = true
            };
            var m2 = new MeasurementDto()
            {
                DateTime = DateTime.Now,
                LessonId = 1,
                PersonId = 16,
                Id = 2,
                SoundCheck = true,
                HeadPose = true,
                OnTop = true
            };
            var measurements = new MeasurementDto[] { m1, m2 };
            string result = await SendMeasurementsToClients (measurements);
            if (result != string.Empty)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, result);
            }
            return Ok();
        }


        /// <summary>
        /// Add a new Measurement to DB
        /// </summary>
        /// <param name="measurementDto">MeasurementDto object contains all of the Measurement's details which will be added to DB</param>
        /// <response code="200">MeasurementDto object contains all of the details from DB</response>
        /// <response code="400">BadRequest - invalid values</response>
        /// <response code="500">InternalServerError - for any error occurred in server</response>
        [HttpPost]
        [ProducesResponseType(typeof(MeasurementDto), 200)]
        [ProducesResponseType(typeof(BadRequestResult), 400)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<MeasurementDto>> Post([FromBody] MeasurementDto measurementDto)
        {
            //validate request
            if (measurementDto == null || measurementDto.PersonId < 1)
            {
                string msg = $"measurementDto is null or person id is invalid";
                _logger.LogError(msg);
                return BadRequest(msg);
            }
            try
            {
                //add measurement to DB
                var measurement = await _measurementRepository.AddMeasurement(measurementDto.ToModel());
                if (measurement == null)
                {
                    string msg = $"cannot add measurement to DB";
                    _logger.LogError(msg);
                    return StatusCode(StatusCodes.Status500InternalServerError, msg);
                }
                else
                {
                    var newMeasurementDto = measurement.ToDto();
                    await SendMeasurementsToClients(new MeasurementDto[] { newMeasurementDto });
                    return Ok(newMeasurementDto);
                }
            }
            catch (Exception e)
            {
                string msg = $"cannot add measurement to DB. due to: {e}";
                _logger.LogError(msg);
                return StatusCode(StatusCodes.Status500InternalServerError, msg);
            }
        }


        /// <summary>
        /// Add a list of new Measurements to DB
        /// </summary>
        /// <param name="measurements">List of MeasurementDto objects, each contains all of the Measurement's details which will be added to DB</param>
        /// <response code="200">List of added MeasurementDto objects, each contains all of the Measurement's details added to DB</response>
        /// <response code="400">BadRequest - invalid values</response>
        /// <response code="500">InternalServerError - for any error occurred in server</response>
        [HttpPost("PostMeasurements")]
        [ProducesResponseType(typeof(MeasurementDto), 200)]
        [ProducesResponseType(typeof(BadRequestResult), 400)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<MeasurementDto>> PostMeasurements([FromBody] List<MeasurementDto> measurements)
        {
            //validate request
            if (measurements == null || measurements.Count == 0)
            {
                string msg = $"measurements are null or empty";
                _logger.LogError(msg);
                return BadRequest(msg);
            }
            try
            {
                var allAddedMeasurement = new List<MeasurementDto>();
                //add measurements to DB
                foreach (var measurement in measurements)
                {
                    var addedMeasurement = await _measurementRepository.AddMeasurement(measurement.ToModel());
                    if (addedMeasurement != null)
                    {
                        allAddedMeasurement.Add(addedMeasurement.ToDto());
                    }
                }
                if (allAddedMeasurement.Count < measurements.Count)
                {
                    throw new Exception($"Not all measurements were added. measurements to add: {measurements.Count}. measurements added: {allAddedMeasurement.Count}");
                }

                await SendMeasurementsToClients(allAddedMeasurement.ToArray());


                return Ok(allAddedMeasurement);
            }
            catch (Exception e)
            {
                string msg = $"cannot add measurements to DB. due to: {e}";
                _logger.LogError(msg);
                return StatusCode(StatusCodes.Status500InternalServerError, msg);
            }
        }

        /// <summary>
        /// Get list of students attendance in specfic lesson in specific time
        /// </summary>
        /// <param name="lessonId">id of the requested lesson</param>
        /// <param name="lessonTime">time of the requested lesson</param>
        /// <response code="200">List of StudentAttendanceDto, each contains person details and its time entrance to the lesson</response>
        /// <response code="400">BadRequest - invalid values</response>
        /// <response code="404">NotFound - cannot find the lesson or any students in the lesson</response>
        /// <response code="500">InternalServerError - for any error occurred in server</response>
        [HttpGet("GetStudentsAttendance/{lessonId}/{lessonTime}")]
        [ProducesResponseType(typeof(List<StudentAttendanceDto>), 200)]
        [ProducesResponseType(typeof(BadRequestResult), 400)]
        [ProducesResponseType(typeof(NotFoundResult), 404)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<List<StudentAttendanceDto>>> GetStudentsAttendance(int lessonId, DateTime lessonTime)
        {
            //validate request
            if (lessonId < 1 || lessonTime == DateTime.MinValue)
            {
                string msg = $"lesson id: {lessonId} or lesson time: {lessonTime} are invalid";
                _logger.LogError(msg);
                return BadRequest(msg);
            }
            try
            {
                //add measurement to DB
                var attendanceList = await _measurementRepository.GetAttendance(lessonId, lessonTime);
                if (attendanceList == null)
                {
                    string msg = $"attendane list is null - cannot get attendance list from DB";
                    _logger.LogError(msg);
                    return NotFound(msg);
                }
                else
                {
                    var studentsAttendance = new List<StudentAttendanceDto>();
                    attendanceList.ForEach(x => studentsAttendance.Add(new StudentAttendanceDto
                    {
                        Person = x.Item1.ToDto(),
                        EntranceTime = x.Item2
                    }));
                    return Ok(studentsAttendance);
                }
            }
            catch (Exception e)
            {
                string msg = $"cannot get attendance list from DB. due to: {e}";
                _logger.LogError(msg);
                return StatusCode(StatusCodes.Status500InternalServerError, msg);
            }
        }

        /// <summary>
        /// Get student's measurements in a given lesson and time
        /// </summary>
        /// <param name="lessonId">id of the requested lesson</param>
        /// <param name="personId">id of the requested student</param>
        /// <param name="lessonTime">time of the requested lesson</param>
        /// <response code="200">List of MeasurementDto, each contains measurements result in specfic time during the lesson</response>
        /// <response code="400">BadRequest - invalid values</response>
        /// <response code="404">NotFound - cannot find the student or any measurements in the lesson</response>
        /// <response code="500">InternalServerError - for any error occurred in server</response>
        [HttpGet("GetStudentMeasurements/{lessonId}/{personId}/{lessonTime}")]
        [ProducesResponseType(typeof(List<MeasurementDto>), 200)]
        [ProducesResponseType(typeof(BadRequestResult), 400)]
        [ProducesResponseType(typeof(NotFoundResult), 404)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<List<MeasurementDto>>> GetStudentMeasurements(int lessonId, int personId, DateTime lessonTime)
        {
            //validate request
            if (lessonId < 1 || personId < 1 || lessonTime == DateTime.MinValue)
            {
                string msg = $"lesson id: {lessonId} or person id: {personId} or lesson time: {lessonTime} are invalid";
                _logger.LogError(msg);
                return BadRequest(msg);
            }
            try
            {
                //get student measurement from DB
                var measurements = await _measurementRepository.GetStudentMeasurements(lessonId, personId, lessonTime);
                if (measurements == null)
                {
                    string msg = $"cannot get measurement from DB";
                    _logger.LogError(msg);
                    return NotFound(msg);
                }
                else
                {
                    return Ok(measurements.ToDto());
                }
            }
            catch (Exception e)
            {
                string msg = $"cannot get measurement from DB. due to: {e}";
                _logger.LogError(msg);
                return StatusCode(StatusCodes.Status500InternalServerError, msg);
            }
        }

        /// <summary>
        /// Get lesson measurements of all students in a given lesson and time
        /// </summary>
        /// <param name="lessonId">id of the requested lesson</param>
        /// <param name="lessonTime">time of the requested lesson</param>
        /// <response code="200">List of MeasurementDto, each contains measurements result in specfic time during the lesson</response>
        /// <response code="400">BadRequest - invalid values</response>
        /// <response code="404">NotFound - cannot find the student or any measurements in the lesson</response>
        /// <response code="500">InternalServerError - for any error occurred in server</response>
        [HttpGet("GetLessonMeasurements/{lessonId}/{lessonTime}")]
        [ProducesResponseType(typeof(List<MeasurementDto>), 200)]
        [ProducesResponseType(typeof(BadRequestResult), 400)]
        [ProducesResponseType(typeof(NotFoundResult), 404)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<List<MeasurementDto>>> GetLessonMeasurements(int lessonId, DateTime lessonTime)
        {
            //validate request
            if (lessonId < 1 || lessonTime == DateTime.MinValue)
            {
                string msg = $"lesson id: {lessonId} or lesson time: {lessonTime} are invalid";
                _logger.LogError(msg);
                return BadRequest(msg);
            }
            try
            {
                //get lesson measurements (of all students) from DB
                var measurements = await _measurementRepository.GetLessonMeasurements(lessonId, lessonTime);
                if (measurements == null)
                {
                    string msg = $"cannot find measurements of lesson id: {lessonId} in DB";
                    _logger.LogError(msg);
                    return NotFound(msg);
                }
                else
                {
                    return Ok(measurements.ToDto());
                }
            }
            catch (Exception e)
            {
                string msg = $"cannot get measurements of lesson id: {lessonId} from DB. due to: {e}";
                _logger.LogError(msg);
                return StatusCode(StatusCodes.Status500InternalServerError, msg);
            }
        }


        /// <summary>
        /// Get lesson history - all the lesson's dates which took place in some dates
        /// </summary>
        /// <param name="lessonId">id of the requested lesson</param>
        /// <response code="200">List of DateTime, each represents a date of lesson which took place</response>
        /// <response code="400">BadRequest - invalid values</response>
        /// <response code="404">NotFound - cannot find the lesson</response>
        /// <response code="500">InternalServerError - for any error occurred in server</response>
        [HttpGet("GetLessonHistory/{lessonId}")]
        [ProducesResponseType(typeof(List<DateTime>), 200)]
        [ProducesResponseType(typeof(BadRequestResult), 400)]
        [ProducesResponseType(typeof(NotFoundResult), 404)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<List<DateTime>>> GetLessonHistory(int lessonId)
        {
            //validate request
            if (lessonId < 1)
            {
                string msg = $"lesson id: {lessonId} must be postive";
                _logger.LogError(msg);
                return BadRequest(msg);
            }
            try
            {
                //get lesson history from DB
                var lessonHistory = await _measurementRepository.GetLessonDates(lessonId);
                if (lessonHistory == null)
                {
                    string msg = $"cannot get lesson history from DB";
                    _logger.LogError(msg);
                    return NotFound(msg);
                }
                else
                {
                    return Ok(lessonHistory);
                }
            }
            catch (Exception e)
            {
                string msg = $"cannot get lesson history from DB. due to: {e}";
                _logger.LogError(msg);
                return StatusCode(StatusCodes.Status500InternalServerError, msg);
            }
        }


        /// <summary>
        /// Delete measurement from DB
        /// </summary>
        /// <param name="measurementId">id of the requested measurement</param>
        /// <response code="200">true - deletion was success</response>
        /// <response code="400">BadRequest - invalid values</response>
        /// <response code="404">NotFound - cannot find the measurement in DB</response>
        /// <response code="500">InternalServerError - for any error occurred in server</response>
        [HttpDelete("{measurementId}")]
        [ProducesResponseType(typeof(bool), 200)]
        [ProducesResponseType(typeof(BadRequestResult), 400)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<bool>> Delete(int measurementId)
        {
            //validate request
            if (measurementId < 0)
            {
                string msg = $"measurement id: {measurementId} is invalid";
                _logger.LogError(msg);
                return BadRequest(msg);
            }
            try
            {
                //delete measurement from DB
                var result = await _measurementRepository.DeleteMeasurement(measurementId);
                if (!result)
                {
                    string msg = $"cannot find measurement id: {measurementId} in DB";
                    _logger.LogError(msg);
                    return NotFound(msg);
                }
                else
                {
                    return Ok(true);
                }
            }
            catch (Exception e)
            {
                string msg = $"cannot delete measurement  from DB. due to: {e}";
                _logger.LogError(msg);
                return StatusCode(StatusCodes.Status500InternalServerError, msg);
            }
        }

        /// <summary>
        /// Send measurements to connected clients
        /// </summary>
        /// <param name="measuremenrts">measurements to send</param>
        /// <returns>Empty string if success, exception string otherwise</returns>
        private async Task<string> SendMeasurementsToClients(MeasurementDto[] measuremenrts)
        {
            string result = string.Empty;
            if (_measurementsHub != null)
            {
                try
                {
                    await _measurementsHub.Send(measuremenrts);
                }
                catch (Exception e)
                {
                    result = $"cannot send measurements to clients. due to: {e}";
                    _logger.LogError(result);
                }
            }

            return result;
        }
    }
}

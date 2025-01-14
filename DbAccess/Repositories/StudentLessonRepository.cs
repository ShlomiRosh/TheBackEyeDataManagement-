﻿using DbAccess.RepositoryInterfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbAccess.Repositories
{
    public class StudentLessonRepository : IStudentLessonRepository
    {
        private readonly BackEyeContext _context;
        private readonly ILogger _logger;
        public StudentLessonRepository(BackEyeContext context, ILogger<LogsRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// get a student lesson from DB by the given lesson id and person id
        /// </summary>
        /// <param name="lessonId">lesson id</param>
        /// <param name="personId">person id</param>
        /// <returns>StudentLessonObject contains relation between a lesson and a person student</returns>
        public async Task<StudentLesson> GetStudentLesson(int? lessonId, int? personId)
        {
            try
            {
                return await _context.StudentLessons.Where(x => x.LessonId == lessonId && x.PersonId == personId).Include(x=>x.Lesson).Include(x=>x.Person).FirstOrDefaultAsync();
            }
            catch (Exception e)
            {
                _logger.LogError($"Cannot get student lesson from DB. lesson id: {lessonId}. person id: {personId} due to: {e}");
                return null;
            }
        }

        /// <summary>
        /// add student lesson to DB - a relation between a student and a lesson
        /// </summary>
        /// <param name="studentLesson">student lesson to add</param>
        /// <returns>the newly added StudentLesson</returns>
        public async Task<StudentLesson> AddStudentLesson(StudentLesson studentLesson)
        {
            try
            {
                var studentLessonFromDb = await GetStudentLesson(studentLesson.LessonId, studentLesson.PersonId);
                if (studentLessonFromDb == null)
                {
                    _context.Add(studentLesson);
                    await _context.SaveChangesAsync();
                    return await GetStudentLesson(studentLesson.LessonId,studentLesson.PersonId);
                }
                else
                {
                    _logger.LogInformation($"Student Lesson with Lesson Id: {studentLessonFromDb.LessonId} already exist");
                }
                return studentLessonFromDb;
            }
            catch (Exception e)
            {
                _logger.LogError($"Cannot add student lesson to DB. due to: {e}");
                return null;
            }
        }

        /// <summary>
        /// delete student lesson from DB by lesson id and person id
        /// </summary>
        /// <param name="lessonId">the lesson id we want to delete</param>
        /// <param name="lessonId">the person id we want to delete</param>
        /// <returns>true if deletion was a success and false otherwise</returns>
        public async Task<bool> DeleteStudentLesson(int lessonId, int personId)
        {
            var tmpStudentLesson = await GetStudentLesson(lessonId, personId);
            if (tmpStudentLesson == null)
            {
                return false;
            }
            try
            {
                _context.StudentLessons.Remove(tmpStudentLesson);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception e)
            {
                _logger.LogError($"Cannot delete student lesson from DB. student lesson id: {tmpStudentLesson.Id}. due to: {e}");
            }
            return false;
        }

        /// <summary>
        /// get list of persons participating in a given lesson
        /// </summary>
        /// <param name="lessonId">lesson id</param>
        /// <returns>list of person object, each one related to the given lesson</returns>
        public async Task<List<Person>> GetStudentsByLessonId(int? lessonId)
        {
            List<Person> students = new List<Person>();
            try
            {
                var studentIds = await _context.StudentLessons.Where(x=>x.LessonId == lessonId).Select(x=>x.PersonId).ToListAsync();
                foreach (var studentId in studentIds)
                {
                    var student = await _context.Persons.Where(x=>x.Id == studentId).FirstOrDefaultAsync();
                    if (student != null)
                    {
                        students.Add(student);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"Cannot get students of lesson from DB. lesson id: {lessonId}. due to: {e}");
            }
            return students;
        }

        /// <summary>
        /// delete all relations StudentLesson of a given person
        /// </summary>
        /// <param name="personId">person id to delete its relations</param>
        /// <returns>true if deletion was a success and false otherwise</returns>
        public async Task<bool> DeleteAllStudentLessons(int? personId)
        {
            try
            {
                var studentlessons = await _context.StudentLessons.Where(sl => sl.PersonId == personId).ToListAsync();

                if (studentlessons == null || studentlessons.Count == 0)
                {
                    throw new Exception($"Measurement of person id: {personId} not found in DB");
                }
                studentlessons.ForEach(sl => _context.StudentLessons.Remove(sl));
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception e)
            {
                _logger.LogError($"Cannot delete student lessons from DB. person id: {personId}. due to: {e}");
            }
            return false;
        }

        /// <summary>
        /// get a student lesson from DB by the given student lesson id
        /// </summary>
        /// <param name="studentLessonId">student lesson id</param>
        /// <returns>StudentLessonObject contains relation between a lesson and a person student</returns>
        public async Task<StudentLesson> GetStudentLessonById(int studentLessonId)
        {
            try
            {
                return await _context.StudentLessons.Where(x => x.Id == studentLessonId).Include(x => x.Lesson).Include(x => x.Person).FirstOrDefaultAsync();
            }
            catch (Exception e)
            {
                _logger.LogError($"Cannot get student lesson from DB. studentLessonId: {studentLessonId} due to: {e}");
                return null;
            }
        }

        /// <summary>
        /// delete a StudentLesson relation of a given studentLessonId
        /// </summary>
        /// <param name="studentLessonId">studentLessonId to delete it</param>
        /// <returns>true if deletion was a success and false otherwise</returns>
        public async Task<bool> DeleteStudentLesson(int studentLessonId)
        {
            try
            {
                var studentLesson = await GetStudentLessonById(studentLessonId);

                if (studentLesson == null)
                {
                    throw new Exception($"studentLesson with id: {studentLessonId} not found in DB");
                }
                
                _context.StudentLessons.Remove(studentLesson);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception e)
            {
                _logger.LogError($"Cannot delete studentLesson from DB. student lesson id: {studentLessonId}. due to: {e}");
            }
            return false;
        }
    }
}

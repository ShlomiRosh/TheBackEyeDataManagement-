﻿using Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dtos
{
    public class LogDto
    {
        public int Id { get; set; }
        public DateTime CreationDate { get; set; }
        public string Data { get; set; }
        public int PersonId { get; set; }
    }
    public static class LogDtoExtension
    {
        public static Log ToModel(this LogDto dto)
        {
            return new Log
            {
                Id = dto.Id,
                CreationDate = dto.CreationDate,
                Data = dto.Data,
                PersonId = dto.PersonId
            };
        }
    }

    public static class LogExtension
    {
        public static LogDto ToDto(this Log model)
        {
            return new LogDto
            {
                Id = model.Id,
                CreationDate = model.CreationDate,
                Data = model.Data,
                PersonId = model.PersonId
            };
        }
        public static List<LogDto> ToDto(this List<Log> model)
        {
            List<LogDto> logs = new List<LogDto>();
            model.ForEach(x => logs.Add(x.ToDto()));
            return logs;
        }
    }
}

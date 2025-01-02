using System.ComponentModel.DataAnnotations;

namespace Api.Models;
public class ProjectInfo
{
	[Required]
	public string Name { get; set; }

	[Required]
	public DateTime Datestamp { get; set; }
}


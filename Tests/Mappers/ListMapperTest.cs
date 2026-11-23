using Api.Mappers;
using Api.Request.List;

namespace Tests.Mappers
{
    public class ListMapperTest
    {
        private readonly ListMapper _mapper = new();

        #region MapBasicList
        [Test]
        public async Task MapBasicList_WhenValidRequestProvided_ShouldMapAllPropertiesTrimSearchTermAndLowerSortBy()
        {
            // Arrange
            var request = new BasicListRequest
            {
                PageNumber = 2,
                PageSize = 25,
                SearchTerm = "   jan kowalski   ",
                SortBy = "CreatedAt",
                SortDescending = true
            };

            // Act
            var result = _mapper.MapBasicList(request);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.PageNumber).IsEqualTo(2);
            await Assert.That(result.PageSize).IsEqualTo(25);
            await Assert.That(result.SearchTerm).IsEqualTo("jan kowalski");
            await Assert.That(result.SortBy).IsEqualTo("createdat");
            await Assert.That(result.SortDescending).IsTrue();
        }

        [Test]
        public async Task MapBasicList_WhenSearchTermAndSortByAreNull_ShouldMapThemAsNull()
        {
            // Arrange
            var request = new BasicListRequest
            {
                PageNumber = 1,
                PageSize = 10,
                SearchTerm = null,
                SortBy = null,
                SortDescending = false
            };

            // Act
            var result = _mapper.MapBasicList(request);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.SearchTerm).IsNull();
            await Assert.That(result.SortBy).IsNull();
            await Assert.That(result.SortDescending).IsFalse();
        }

        [Test]
        public async Task MapBasicList_WhenSearchTermContainsOnlyWhitespace_ShouldTrimToEmptyString()
        {
            // Arrange
            var request = new BasicListRequest
            {
                SearchTerm = "     "
            };

            // Act
            var result = _mapper.MapBasicList(request);

            // Assert
            await Assert.That(result.SearchTerm).IsEqualTo(string.Empty);
        }
        #endregion

        #region MapUserList
        [Test]
        public async Task MapUserList_WhenValidRequestProvided_ShouldMapAllPropertiesTrimSearchTermAndLowerSortBy()
        {
            // Arrange
            var request = new UserListRequest
            {
                PageNumber = 3,
                PageSize = 50,
                SearchTerm = "  anna@test.pl  ",
                SortBy = "FirstName",
                SortDescending = false,
                IncludeBanned = true,
                IncludeAdmins = true
            };

            // Act
            var result = _mapper.MapUserList(request);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.PageNumber).IsEqualTo(3);
            await Assert.That(result.PageSize).IsEqualTo(50);
            await Assert.That(result.SearchTerm).IsEqualTo("anna@test.pl");
            await Assert.That(result.SortBy).IsEqualTo("firstname");
            await Assert.That(result.SortDescending).IsFalse();
            await Assert.That(result.IncludeBanned).IsTrue();
            await Assert.That(result.IncludeAdmins).IsTrue();
        }

        [Test]
        public async Task MapUserList_WhenOptionalPropertiesAreNullAndFlagsDefault_ShouldMapCorrectly()
        {
            // Arrange
            var request = new UserListRequest
            {
                PageNumber = 1,
                PageSize = 10,
                SearchTerm = null,
                SortBy = null,
                SortDescending = false,
                IncludeBanned = false,
                IncludeAdmins = false
            };

            // Act
            var result = _mapper.MapUserList(request);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.PageNumber).IsEqualTo(1);
            await Assert.That(result.PageSize).IsEqualTo(10);
            await Assert.That(result.SearchTerm).IsNull();
            await Assert.That(result.SortBy).IsNull();
            await Assert.That(result.SortDescending).IsFalse();
            await Assert.That(result.IncludeBanned).IsFalse();
            await Assert.That(result.IncludeAdmins).IsFalse();
        }
        #endregion

        #region ToReportList
        [Test]
        public async Task ToReportList_WhenValidRequestProvided_ShouldMapAllPropertiesTrimSearchTermAndLowerSortBy()
        {
            // Arrange
            var request = new ReportListRequest
            {
                PageNumber = 2,
                PageSize = 15,
                SearchTerm = "  spam content  ",
                SortBy = "ResolvedAt",
                SortDescending = true,
                IsShowAll = true
            };

            // Act
            var result = _mapper.ToReportList(request);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.PageNumber).IsEqualTo(2);
            await Assert.That(result.PageSize).IsEqualTo(15);
            await Assert.That(result.SearchTerm).IsEqualTo("spam content");
            await Assert.That(result.SortBy).IsEqualTo("resolvedat");
            await Assert.That(result.SortDescending).IsTrue();
            await Assert.That(result.IsShowAll).IsTrue();
        }

        [Test]
        public async Task ToReportList_WhenOptionalPropertiesAreNullAndFlagsDefault_ShouldMapCorrectly()
        {
            // Arrange
            var request = new ReportListRequest
            {
                PageNumber = 1,
                PageSize = 10,
                SearchTerm = null,
                SortBy = null,
                SortDescending = false,
                IsShowAll = false
            };

            // Act
            var result = _mapper.ToReportList(request);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.PageNumber).IsEqualTo(1);
            await Assert.That(result.PageSize).IsEqualTo(10);
            await Assert.That(result.SearchTerm).IsNull();
            await Assert.That(result.SortBy).IsNull();
            await Assert.That(result.SortDescending).IsFalse();
            await Assert.That(result.IsShowAll).IsFalse();
        }
        #endregion

        #region ToSimpleList
        [Test]
        public async Task ToSimpleList_WhenValidRequestProvided_ShouldMapAllPropertiesAndLowerSortBy()
        {
            // Arrange
            var request = new SimpleListRequest
            {
                PageNumber = 4,
                PageSize = 20,
                SortBy = "Status",
                SortDescending = true
            };

            // Act
            var result = _mapper.ToSimpleList(request);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.PageNumber).IsEqualTo(4);
            await Assert.That(result.PageSize).IsEqualTo(20);
            await Assert.That(result.SortBy).IsEqualTo("status");
            await Assert.That(result.SortDescending).IsTrue();
        }

        [Test]
        public async Task ToSimpleList_WhenOptionalPropertiesAreNull_ShouldMapSortByAsNull()
        {
            // Arrange
            var request = new SimpleListRequest
            {
                PageNumber = null,
                PageSize = null,
                SortBy = null,
                SortDescending = false
            };

            // Act
            var result = _mapper.ToSimpleList(request);

            // Assert
            await Assert.That(result).IsNotNull();
            await Assert.That(result.PageNumber).IsNull();
            await Assert.That(result.PageSize).IsNull();
            await Assert.That(result.SortBy).IsNull();
            await Assert.That(result.SortDescending).IsFalse();
        }
        #endregion
    }
}
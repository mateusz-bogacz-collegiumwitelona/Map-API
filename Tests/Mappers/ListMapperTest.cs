using Api.Mappers;
using Api.Request.List;

namespace Tests.Mappers
{
    public class ListMapperTest
    {
        private readonly ListMapper _mapper = new();

        #region MapBasicList
        [Test]
        public async Task MapBasicList_WhenValidRequestProvided_ShouldMapAllPropertiesAndTrimSearchTerm()
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
            await Assert.That(result.SortBy).IsEqualTo("CreatedAt");
            await Assert.That(result.SortDescending).IsTrue();
        }

        [Test]
        public async Task MapBasicList_WhenSearchTermIsNull_ShouldMapSearchTermAsNull()
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
        public async Task MapUserList_WhenValidRequestProvided_ShouldMapAllPropertiesAndTrimSearchTerm()
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
            await Assert.That(result.SortBy).IsEqualTo("FirstName");
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
    }
}
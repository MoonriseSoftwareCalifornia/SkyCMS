/**
 * Blog Stream Loader
 * 
 * Orchestrates client-side rendering of blog streams with embedded JSON metadata.
 * Handles post content fetching, pagination, and dynamic DOM insertion.
 * 
 * Expects:
 * - Script tag with id="blog-posts-meta" containing JSON array of post metadata
 * - Div with id="post-list" for post content insertion
 * - Ul with id="pagination" for navigation controls
 */

class BlogStreamLoader {
  constructor(pageSize = 10) {
    this.pageSize = pageSize;
    this.posts = [];
    this.currentPage = 1;
    this.init();
  }

  /**
   * Initializes the loader and renders initial page
   */
  init() {
    try {
      // Parse embedded JSON metadata
      const metaElement = document.getElementById('blog-posts-meta');
      if (!metaElement) {
        console.error('BlogStreamLoader: Missing #blog-posts-meta element');
        return;
      }

      this.posts = JSON.parse(metaElement.textContent);
      if (!Array.isArray(this.posts)) {
        throw new Error('blog-posts-meta must contain a JSON array');
      }

      // Get current page from URL query param
      const params = new URLSearchParams(window.location.search);
      const page = parseInt(params.get('page')) || 1;
      this.currentPage = Math.max(1, page);

      // Validate page number
      const totalPages = this.getTotalPages();
      if (this.currentPage > totalPages && totalPages > 0) {
        this.currentPage = totalPages;
      }

      // Render the current page
      this.render();
    } catch (error) {
      console.error('BlogStreamLoader initialization error:', error);
      this.showError('Failed to load blog posts');
    }
  }

  /**
   * Gets total number of pages
   */
  getTotalPages() {
    return Math.ceil(this.posts.length / this.pageSize);
  }

  /**
   * Gets posts for the current page
   */
  getCurrentPagePosts() {
    const start = (this.currentPage - 1) * this.pageSize;
    const end = start + this.pageSize;
    return this.posts.slice(start, end);
  }

  /**
   * Renders the current page (post content + pagination)
   */
  async render() {
    await this.renderPost();
    this.renderPagination();
  }

  /**
   * Renders the first post of the current page
   */
  async renderPost() {
    const posts = this.getCurrentPagePosts();
    const postContainer = document.getElementById('post-list');

    if (!postContainer) {
      console.error('BlogStreamLoader: Missing #post-list element');
      return;
    }

    if (posts.length === 0) {
      postContainer.innerHTML = '<p>No blog posts available.</p>';
      return;
    }

    const post = posts[0]; // Display first post of the page

    try {
      // Fetch post content by URL
      const response = await fetch(`${post.urlPath}.html`);
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }

      const html = await response.text();
      postContainer.innerHTML = html;
    } catch (error) {
      console.error(`Failed to load post "${post.title}":`, error);
      postContainer.innerHTML = `
        <article class="sky-blog-post-article">
          <h2 class="sky-blog-post-title">${this.escapeHtml(post.title)}</h2>
          <p class="text-danger">Failed to load post content. Please try again later.</p>
        </article>
      `;
    }
  }

  /**
   * Renders pagination controls
   */
  renderPagination() {
    const paginationContainer = document.getElementById('pagination');
    if (!paginationContainer) {
      console.error('BlogStreamLoader: Missing #pagination element');
      return;
    }

    const totalPages = this.getTotalPages();
    const isFirstPage = this.currentPage === 1;
    const isLastPage = this.currentPage >= totalPages;

    let html = '';

    // "Newer" button
    html += '<li class="sky-blog-stream-nav-item';
    if (isFirstPage) html += ' sky-blog-stream-nav-item-disabled';
    html += '">';
    if (!isFirstPage) {
      html += `<a href="?page=${this.currentPage - 1}" class="sky-blog-stream-nav-link" aria-label="Previous">« Newer</a>`;
    } else {
      html += '<span class="sky-blog-stream-nav-link" aria-hidden="true">« Newer</span>';
    }
    html += '</li>';

    // "Older" button
    html += '<li class="sky-blog-stream-nav-item';
    if (isLastPage) html += ' sky-blog-stream-nav-item-disabled';
    html += '">';
    if (!isLastPage) {
      html += `<a href="?page=${this.currentPage + 1}" class="sky-blog-stream-nav-link" aria-label="Next">Older »</a>`;
    } else {
      html += '<span class="sky-blog-stream-nav-link" aria-hidden="true">Older »</span>';
    }
    html += '</li>';

    paginationContainer.innerHTML = html;
  }

  /**
   * Shows an error message to the user
   */
  showError(message) {
    const postContainer = document.getElementById('post-list');
    if (postContainer) {
      postContainer.innerHTML = `<p class="alert alert-danger">${this.escapeHtml(message)}</p>`;
    }
  }

  /**
   * Escapes HTML special characters to prevent XSS
   */
  escapeHtml(unsafe) {
    return unsafe
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
  }
}

// Auto-initialize when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
  new BlogStreamLoader();
});

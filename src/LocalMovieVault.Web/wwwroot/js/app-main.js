(function () {
  function byId(id) {
    return document.getElementById(id);
  }

  function getRequestVerificationToken() {
    return document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
  }

  function normalizeGenres(csv) {
    return String(csv || '')
      .split(/[;,/|]/)
      .map((value) => value.trim().toLowerCase())
      .filter(Boolean);
  }

  function normalizeTags(csv) {
    return String(csv || '')
      .split(/[;,]/)
      .map((value) => value.trim())
      .filter(Boolean);
  }

  function openModal(modal) {
    if (!modal) return;
    modal.classList.remove('is-hidden');
    modal.setAttribute('aria-hidden', 'false');
  }

  function closeModal(modal) {
    if (!modal) return;
    modal.classList.add('is-hidden');
    modal.setAttribute('aria-hidden', 'true');
  }

  function saveReviewScrollPosition() {
    try {
      sessionStorage.setItem('mymoviedb:reviewScrollY', String(window.scrollY || 0));
    } catch (_) {
      // Ignore storage issues.
    }
  }

  function restoreReviewScrollPosition() {
    try {
      const raw = sessionStorage.getItem('mymoviedb:reviewScrollY');
      if (!raw) return;

      sessionStorage.removeItem('mymoviedb:reviewScrollY');
      const value = Number(raw);
      if (Number.isFinite(value) && value >= 0) {
        window.requestAnimationFrame(() => window.scrollTo(0, value));
      }
    } catch (_) {
      // Ignore storage issues.
    }
  }

  function resetReasonChecks(scope) {
    scope.querySelectorAll('.reason-check input[type="checkbox"]').forEach((checkbox) => {
      checkbox.checked = false;
    });
    scope.querySelectorAll('.reason-check').forEach((item) => {
      item.classList.remove('is-active');
    });
  }

  function updateReasonCheckStates(scope) {
    scope.querySelectorAll('.reason-check').forEach((item) => {
      const input = item.querySelector('input[type="checkbox"]');
      item.classList.toggle('is-active', !!input?.checked);
    });
  }

  function syncGenreTagVisibility(form, genres) {
    const genreSection = form.querySelector('[data-tag-section="genre"]');
    if (!genreSection) return;

    let visibleCount = 0;
    genreSection.querySelectorAll('[data-tag-kind="genre"]').forEach((item) => {
      const supportedGenres = normalizeGenres(item.dataset.tagGenres || '');
      const visible = supportedGenres.some((genre) => genres.includes(genre));
      item.classList.toggle('is-hidden', !visible);
      if (!visible) {
        const input = item.querySelector('input[type="checkbox"]');
        if (input) {
          input.checked = false;
        }
        item.classList.remove('is-active');
      } else {
        visibleCount += 1;
      }
    });

    genreSection.classList.toggle('is-hidden', visibleCount === 0);
  }

  function prefillWatchForm(trigger) {
    const form = byId('watchFeedbackForm');
    if (!form) return;
    const watchedState = trigger.dataset.watched || 'unwatched';

    byId('watchFeedbackMovieId').value = trigger.dataset.movieId || '';
    byId('watchFeedbackTitle').textContent = watchedState === 'watched'
      ? 'Review: ' + (trigger.dataset.movieTitle || 'movie')
      : 'Mark watched: ' + (trigger.dataset.movieTitle || 'movie');

    const returnUrl = byId('watchFeedbackReturnUrl');
    if (returnUrl) {
      returnUrl.value = trigger.dataset.returnUrl || (window.location.pathname + window.location.search);
    }

    const reviewTabUrl = byId('watchFeedbackReviewTabUrl');
    if (reviewTabUrl) {
      reviewTabUrl.value = trigger.dataset.reviewTabUrl || '/Movies?section=review';
    }

    const reviewLaterButton = byId('watchFeedbackReviewLater');
    if (reviewLaterButton) {
      reviewLaterButton.classList.toggle('is-hidden', watchedState === 'watched');
      reviewLaterButton.dataset.movieId = trigger.dataset.movieId || '';
      reviewLaterButton.dataset.returnUrl = trigger.dataset.returnUrl || (window.location.pathname + window.location.search);
      reviewLaterButton.dataset.reviewTabUrl = trigger.dataset.reviewTabUrl || '/Movies?section=review';
    }

    const verdict = byId('watchFeedbackVerdict');
    if (verdict) {
      verdict.value = trigger.dataset.primaryVerdict || '';
    }

    resetReasonChecks(form);
    const selectedTags = normalizeTags(trigger.dataset.reasonTags);
    form.querySelectorAll('.reason-check input[type="checkbox"]').forEach((checkbox) => {
      checkbox.checked = selectedTags.includes(checkbox.value);
    });

    syncGenreTagVisibility(form, normalizeGenres(trigger.dataset.movieGenre));
    updateReasonCheckStates(form);
  }

  async function postForm(url, body) {
    const response = await fetch(url, {
      method: 'POST',
      headers: { 'X-Requested-With': 'XMLHttpRequest' },
      body
    });

    const payload = await response.json().catch(async () => {
      const text = await response.text();
      throw new Error(text || 'Request failed.');
    });
    if (!response.ok || !payload.success) {
      throw new Error(payload.message || 'Request failed.');
    }

    return payload;
  }

  async function handleMismatchSuggestion(payload) {
    if (!payload || !payload.mismatchSuggestion || !payload.mismatchSuggestion.prompt) {
      if (payload && payload.showMismatchPopup && payload.message) {
        window.alert(payload.message);
      }

      return;
    }

    const modal = byId('mismatchSuggestionModal');
    const prompt = byId('mismatchSuggestionPrompt');
    if (!modal || !prompt) {
      const accepted = window.confirm(payload.mismatchSuggestion.prompt);
      if (!accepted) {
        return;
      }

      const fallbackFormData = new FormData();
      fallbackFormData.append('__RequestVerificationToken', getRequestVerificationToken());
      fallbackFormData.append('kind', payload.mismatchSuggestion.kind || '');
      fallbackFormData.append('value', payload.mismatchSuggestion.value || '');
      fallbackFormData.append('label', payload.mismatchSuggestion.label || '');
      fallbackFormData.append('direction', payload.mismatchSuggestion.direction || '');
      fallbackFormData.append('response', 'accept');
      await postForm('/Movies/ApplyMismatchPreferenceSuggestion', fallbackFormData);
      return;
    }

    prompt.textContent = payload.mismatchSuggestion.prompt;
    const decision = await new Promise((resolve) => {
      const accept = byId('mismatchSuggestionAccept');
      const reject = byId('mismatchSuggestionReject');
      const cancel = byId('mismatchSuggestionCancel');
      const closeButtons = modal.querySelectorAll('.js-close-modal');

      function cleanup(result) {
        if (accept) accept.onclick = null;
        if (reject) reject.onclick = null;
        if (cancel) cancel.onclick = null;
        closeButtons.forEach((button) => {
          button.onclick = null;
        });
        modal.onclick = null;
        closeModal(modal);
        resolve(result);
      }

      if (accept) accept.onclick = () => cleanup('accept');
      if (reject) reject.onclick = () => cleanup('reject');
      if (cancel) cancel.onclick = () => cleanup('cancel');
      closeButtons.forEach((button) => {
        button.onclick = () => cleanup('cancel');
      });
      modal.onclick = (event) => {
        if (event.target === modal) {
          cleanup('cancel');
        }
      };

      openModal(modal);
    });

    if (decision === 'cancel') {
      return;
    }

    const formData = new FormData();
    formData.append('__RequestVerificationToken', getRequestVerificationToken());
    formData.append('kind', payload.mismatchSuggestion.kind || '');
    formData.append('value', payload.mismatchSuggestion.value || '');
    formData.append('label', payload.mismatchSuggestion.label || '');
    formData.append('direction', payload.mismatchSuggestion.direction || '');
    formData.append('response', decision === 'accept' ? 'accept' : 'reject');

    await postForm('/Movies/ApplyMismatchPreferenceSuggestion', formData);
  }

  function submitFallbackPost(url, fields) {
    const form = document.createElement('form');
    form.method = 'POST';
    form.action = url;
    form.style.display = 'none';

    Object.entries(fields).forEach(([key, value]) => {
      const input = document.createElement('input');
      input.type = 'hidden';
      input.name = key;
      input.value = value || '';
      form.appendChild(input);
    });

    document.body.appendChild(form);
    form.submit();
  }

  function populateToggleWatchedForm(trigger) {
    const form = byId('toggleWatchedForm');
    if (!form) return null;

    const movieId = trigger.dataset.movieId || '';
    const returnUrl = trigger.dataset.returnUrl || (window.location.pathname + window.location.search);

    const movieIdInput = byId('toggleWatchedMovieId');
    if (movieIdInput) {
      movieIdInput.value = movieId;
    }

    const returnUrlInput = byId('toggleWatchedReturnUrl');
    if (returnUrlInput) {
      returnUrlInput.value = returnUrl;
    }

    return form;
  }

  async function toggleToUnwatched(trigger) {
    if (!window.confirm('Mark this movie as unwatched?')) {
      return;
    }

    const form = populateToggleWatchedForm(trigger);
    if (!form) {
      throw new Error('Toggle form not found.');
    }

    await postForm(form.action, new FormData(form));
    saveReviewScrollPosition();
    window.location.reload();
  }

  function bindReasonPickers() {
    document.querySelectorAll('.tag-picker-sections').forEach((container) => {
      const maxTags = Number(container.dataset.maxTags || '6');
      container.querySelectorAll('input[type="checkbox"]').forEach((checkbox) => {
        checkbox.addEventListener('change', () => {
          const checked = Array.from(container.querySelectorAll('input[type="checkbox"]:checked'));
          if (checked.length > maxTags) {
            checkbox.checked = false;
            return;
          }

          updateReasonCheckStates(container);
        });
      });
    });
  }

  function bindModalButtons() {
    const watchModal = byId('watchFeedbackModal');
    const dismissModal = byId('dismissMovieModal');

    document.querySelectorAll('.js-open-watch-modal').forEach((trigger) => {
      trigger.addEventListener('click', (event) => {
        if (trigger.tagName === 'A') {
          event.preventDefault();
        }

        prefillWatchForm(trigger);
        openModal(watchModal);
      });
    });

    document.querySelectorAll('.js-toggle-unwatched').forEach((trigger) => {
      trigger.addEventListener('click', async (event) => {
        event.preventDefault();
        const toggleForm = populateToggleWatchedForm(trigger);
        try {
          await toggleToUnwatched(trigger);
        } catch (_) {
          if (toggleForm) {
            toggleForm.requestSubmit();
            return;
          }

          submitFallbackPost('/Movies/ToggleWatched', {
            __RequestVerificationToken: getRequestVerificationToken(),
            id: trigger.dataset.movieId || '',
            returnUrl: trigger.dataset.returnUrl || (window.location.pathname + window.location.search)
          });
        }
      });
    });

    document.querySelectorAll('.js-open-dismiss-modal').forEach((trigger) => {
      trigger.addEventListener('click', () => {
        byId('dismissMovieId').value = trigger.dataset.movieId || '';
        byId('dismissMovieTitle').textContent = 'Dismiss: ' + (trigger.dataset.movieTitle || 'movie');
        openModal(dismissModal);
      });
    });

    document.querySelectorAll('.js-close-modal').forEach((button) => {
      button.addEventListener('click', () => {
        closeModal(watchModal);
        closeModal(dismissModal);
      });
    });

    document.querySelectorAll('.app-modal').forEach((modal) => {
      modal.addEventListener('click', (event) => {
        if (event.target === modal) {
          closeModal(modal);
        }
      });
    });
  }

  function bindWatchFeedbackSubmit() {
    const form = byId('watchFeedbackForm');
    if (!form) return;

    form.addEventListener('submit', async (event) => {
      event.preventDefault();

      try {
        const payload = await postForm(form.action, new FormData(form));
        try {
          await handleMismatchSuggestion(payload);
        } catch (_) {
          window.alert('Watched feedback was saved, but the extra recommendation signal could not be stored.');
        }

        closeModal(byId('watchFeedbackModal'));
        saveReviewScrollPosition();
        window.location.reload();
      } catch (_) {
        window.alert('Could not save watched feedback.');
      }
    });
  }

  function bindReviewLaterSubmit() {
    const button = byId('watchFeedbackReviewLater');
    if (!button) return;

    button.addEventListener('click', async () => {
      const formData = new FormData();
      formData.append('__RequestVerificationToken', getRequestVerificationToken());
      formData.append('id', button.dataset.movieId || '');
      formData.append('returnUrl', button.dataset.returnUrl || (window.location.pathname + window.location.search));

      try {
        const payload = await postForm('/Movies/QueueForReview', formData);
        closeModal(byId('watchFeedbackModal'));
        window.location.assign(payload.redirectUrl || button.dataset.reviewTabUrl || '/Movies?section=review');
      } catch (_) {
        window.alert('Could not move the movie to review.');
      }
    });
  }

  function bindDismissSubmit() {
    const form = byId('dismissMovieForm');
    if (!form) return;

    form.addEventListener('submit', async (event) => {
      event.preventDefault();

      try {
        await postForm(form.action, new FormData(form));
        window.location.reload();
      } catch (_) {
        window.alert('Could not dismiss the movie.');
      }
    });
  }

  function bindSettingsPreview() {
    const form = byId('settingsForm');
    const previewHost = byId('settingsPreviewHost');
    if (!form || !previewHost) return;

    const prioritySelects = Array.from(form.querySelectorAll('[data-taste-priority-select]'));
    const applyButton = byId('settingsApplyButton');
    const randomizeButton = byId('settingsRandomizeButton');
    let previewRequestId = 0;

    function syncDuplicateOptions() {
      const chosen = prioritySelects
        .map((select) => select.value)
        .filter(Boolean);

      prioritySelects.forEach((select) => {
        Array.from(select.options).forEach((option) => {
          if (!option.value) {
            option.disabled = false;
            return;
          }

          option.disabled = option.value !== select.value && chosen.includes(option.value);
        });
      });
    }

    async function refreshPreview() {
      const requestId = ++previewRequestId;
      const params = new URLSearchParams();
      new FormData(form).forEach((value, key) => {
        if (key === '__RequestVerificationToken') {
          return;
        }

        params.append(key, value);
      });

      try {
        const response = await fetch('/Settings/Preview?' + params.toString(), {
          headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });

        if (!response.ok || requestId !== previewRequestId) {
          return;
        }

        previewHost.innerHTML = await response.text();
      } catch (_) {
        // Ignore preview refresh failures and keep the current card rendered.
      }
    }

    syncDuplicateOptions();
    prioritySelects.forEach((select) => {
      select.addEventListener('change', () => {
        syncDuplicateOptions();
        refreshPreview();
      });
    });

    form.addEventListener('submit', () => {
      if (applyButton) {
        applyButton.disabled = true;
        applyButton.textContent = 'Recalculating predictions...';
      }
    });

    if (randomizeButton) {
      randomizeButton.addEventListener('click', () => {
        const params = new URLSearchParams();
        new FormData(form).forEach((value, key) => {
          if (key === '__RequestVerificationToken') {
            return;
          }

          params.append(key, value);
        });

        params.delete('previewMovieId');
        params.set('randomize', 'true');
        window.location.assign((randomizeButton.dataset.randomizeUrl || '/Settings') + '?' + params.toString());
      });
    }
  }

  document.addEventListener('DOMContentLoaded', () => {
    restoreReviewScrollPosition();
    bindReasonPickers();
    bindModalButtons();
    bindWatchFeedbackSubmit();
    bindReviewLaterSubmit();
    bindDismissSubmit();
    bindSettingsPreview();
  });
})();

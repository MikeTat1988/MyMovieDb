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

  function setAddMovieReasonTags(tags) {
    const container = byId('addMovieReasonTagsContainer');
    if (!container) return;
    container.innerHTML = '';
    tags.forEach((tag) => {
      const input = document.createElement('input');
      input.type = 'hidden';
      input.name = 'addReasonTags';
      input.value = tag;
      container.appendChild(input);
    });
  }

  function resetAddMovieReviewState() {
    const verdictInput = byId('addMovieVerdict');
    const queueInput = byId('addMovieQueueForReview');
    if (verdictInput) {
      verdictInput.value = '';
    }
    if (queueInput) {
      queueInput.value = 'false';
    }
    setAddMovieReasonTags([]);
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
    form.dataset.forceRedirectUrl = trigger.dataset.forceRedirectUrl || '';

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
      const allowReviewLater = String(trigger.dataset.allowReviewLater || '').toLowerCase() === 'true';
      reviewLaterButton.classList.toggle('is-hidden', watchedState === 'watched' && !allowReviewLater);
      reviewLaterButton.dataset.movieId = trigger.dataset.movieId || '';
      reviewLaterButton.dataset.returnUrl = trigger.dataset.returnUrl || (window.location.pathname + window.location.search);
      reviewLaterButton.dataset.reviewTabUrl = trigger.dataset.reviewTabUrl || '/Movies?section=review';
      reviewLaterButton.textContent = allowReviewLater ? 'Save for review' : 'Review later';
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

  function populateDismissForm(trigger) {
    const form = byId('dismissMovieForm');
    if (!form) return null;

    const movieId = trigger.dataset.movieId || '';
    const returnUrl = trigger.dataset.returnUrl || (window.location.pathname + window.location.search);

    const movieIdInput = byId('dismissMovieId');
    if (movieIdInput) {
      movieIdInput.value = movieId;
    }

    const title = byId('dismissMovieTitle');
    if (title) {
      title.textContent = 'Dismiss: ' + (trigger.dataset.movieTitle || 'movie');
    }

    const returnUrlInput = byId('dismissMovieReturnUrl');
    if (returnUrlInput) {
      returnUrlInput.value = returnUrl;
    }

    return form;
  }

  function populateToggleWowForm(trigger) {
    const form = byId('toggleWowForm');
    if (!form) return null;

    const movieId = trigger.dataset.movieId || '';
    const returnUrl = trigger.dataset.returnUrl || (window.location.pathname + window.location.search);

    const movieIdInput = byId('toggleWowMovieId');
    if (movieIdInput) {
      movieIdInput.value = movieId;
    }

    const returnUrlInput = byId('toggleWowReturnUrl');
    if (returnUrlInput) {
      returnUrlInput.value = returnUrl;
    }

    return form;
  }

  function populateRestoreForm(trigger) {
    const form = byId('restoreMovieForm');
    if (!form) return null;

    const movieId = trigger.dataset.movieId || '';
    const returnUrl = trigger.dataset.returnUrl || (window.location.pathname + window.location.search);

    const movieIdInput = byId('restoreMovieId');
    if (movieIdInput) {
      movieIdInput.value = movieId;
    }

    const returnUrlInput = byId('restoreMovieReturnUrl');
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

  async function restoreDismissedMovie(trigger) {
    if (!window.confirm('Restore this movie from dismissed?')) {
      return;
    }

    const form = populateRestoreForm(trigger);
    if (!form) {
      throw new Error('Restore form not found.');
    }

    await postForm(form.action, new FormData(form));
    window.location.reload();
  }

  async function toggleWowPick(trigger) {
    const form = populateToggleWowForm(trigger);
    if (!form) {
      throw new Error('Toggle wow form not found.');
    }

    const payload = await postForm(form.action, new FormData(form));
    if (!payload.success) {
      throw new Error(payload.message || 'Could not update wow pick.');
    }

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

  function bindSegmentedControls() {
    document.querySelectorAll('[data-segmented-control]').forEach((container) => {
      const syncState = () => {
        container.querySelectorAll('.segment-button').forEach((button) => {
          const input = button.querySelector('input[type="radio"]');
          button.classList.toggle('is-active', !!input?.checked);
        });
      };

      container.querySelectorAll('input[type="radio"]').forEach((input) => {
        input.addEventListener('change', syncState);
      });

      syncState();
    });
  }

  function bindModalButtons() {
    const watchModal = byId('watchFeedbackModal');
    const dismissModal = byId('dismissMovieModal');
    const estimateModal = byId('estimatePreviewModal');

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

    document.querySelectorAll('.js-open-dismiss-modal, .js-toggle-dismiss-state').forEach((trigger) => {
      trigger.addEventListener('click', async (event) => {
        event.preventDefault();

        const dismissed = String(trigger.dataset.dismissed || '').toLowerCase() === 'true';
        if (dismissed) {
          const restoreForm = populateRestoreForm(trigger);
          try {
            await restoreDismissedMovie(trigger);
          } catch (_) {
            if (restoreForm) {
              restoreForm.requestSubmit();
              return;
            }

            submitFallbackPost('/Movies/Restore', {
              __RequestVerificationToken: getRequestVerificationToken(),
              id: trigger.dataset.movieId || '',
              returnUrl: trigger.dataset.returnUrl || (window.location.pathname + window.location.search)
            });
          }

          return;
        }

        populateDismissForm(trigger);
        openModal(dismissModal);
      });
    });

    document.querySelectorAll('.js-toggle-wow-state').forEach((trigger) => {
      trigger.addEventListener('click', async (event) => {
        event.preventDefault();
        const wowForm = populateToggleWowForm(trigger);
        try {
          await toggleWowPick(trigger);
        } catch (_) {
          if (wowForm) {
            wowForm.requestSubmit();
            return;
          }

          submitFallbackPost('/Movies/ToggleWow', {
            __RequestVerificationToken: getRequestVerificationToken(),
            id: trigger.dataset.movieId || '',
            returnUrl: trigger.dataset.returnUrl || (window.location.pathname + window.location.search)
          });
        }
      });
    });

    document.querySelectorAll('.js-close-modal').forEach((button) => {
      button.addEventListener('click', () => {
        closeModal(watchModal);
        closeModal(dismissModal);
        closeModal(estimateModal);
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

      if (String(form.dataset.createMode || '').toLowerCase() === 'true') {
        const addForm = byId(form.dataset.addMovieFormId || '');
        if (!addForm) {
          window.alert('Could not find the add form.');
          return;
        }

        const verdict = byId('watchFeedbackVerdict')?.value || '';
        if (!verdict) {
          window.alert('Choose a grade first.');
          return;
        }

        const selectedTags = Array.from(form.querySelectorAll('input[name="reasonTags"]:checked')).map((input) => input.value);
        const verdictInput = byId('addMovieVerdict');
        const queueInput = byId('addMovieQueueForReview');
        if (verdictInput) {
          verdictInput.value = verdict;
        }
        if (queueInput) {
          queueInput.value = 'false';
        }
        setAddMovieReasonTags(selectedTags);
        closeModal(byId('watchFeedbackModal'));
        addForm.requestSubmit();
        return;
      }

      try {
        const payload = await postForm(form.action, new FormData(form));
        try {
          await handleMismatchSuggestion(payload);
        } catch (_) {
          window.alert('Watched feedback was saved, but the extra recommendation signal could not be stored.');
        }

        closeModal(byId('watchFeedbackModal'));
        const forcedRedirectUrl = form.dataset.forceRedirectUrl || '';
        if (forcedRedirectUrl) {
          window.location.assign(forcedRedirectUrl);
          return;
        }

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
      const addMovieFormId = button.dataset.addMovieFormId || '';
      if (addMovieFormId) {
        const addForm = byId(addMovieFormId);
        if (!addForm) {
          window.alert('Could not find the add form.');
          return;
        }

        const verdictInput = byId('addMovieVerdict');
        const queueInput = byId('addMovieQueueForReview');
        if (verdictInput) {
          verdictInput.value = '';
        }
        if (queueInput) {
          queueInput.value = 'true';
        }
        setAddMovieReasonTags([]);
        closeModal(byId('watchFeedbackModal'));
        addForm.requestSubmit();
        return;
      }

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

  function bindRestoreSubmit() {
    const form = byId('restoreMovieForm');
    if (!form) return;

    form.addEventListener('submit', async (event) => {
      event.preventDefault();

      try {
        await postForm(form.action, new FormData(form));
        window.location.reload();
      } catch (_) {
        window.alert('Could not restore the movie.');
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

  function bindAddMovieFlow() {
    const addForm = byId('addMovieSaveForm');
    const watchModal = byId('watchFeedbackModal');
    if (!addForm || !watchModal) return;

    const watchedRadio = byId('addMovieWatched');
    const notWatchedRadio = byId('addMovieNotWatched');
    const reviewLaterButton = byId('watchFeedbackReviewLater');

    function openAddMovieReviewModal() {
      const fakeTrigger = {
        dataset: {
          movieId: '',
          movieTitle: addForm.querySelector('input[name="Movie.Title"]')?.value || 'movie',
          movieGenre: addForm.querySelector('input[name="Movie.GenresCsv"]')?.value || '',
          primaryVerdict: byId('addMovieVerdict')?.value || '',
          reasonTags: Array.from(addForm.querySelectorAll('input[name="addReasonTags"]')).map((input) => input.value).join(', '),
          watched: 'watched',
          allowReviewLater: 'true',
          returnUrl: '/Movies?section=watched'
        }
      };

      prefillWatchForm(fakeTrigger);
      const watchForm = byId('watchFeedbackForm');
      if (watchForm) {
        watchForm.dataset.createMode = 'true';
        watchForm.dataset.addMovieFormId = addForm.id;
      }
      if (reviewLaterButton) {
        reviewLaterButton.dataset.addMovieFormId = addForm.id;
        reviewLaterButton.textContent = 'Save for review';
      }
      openModal(watchModal);
    }

    function clearAddMode() {
      const watchForm = byId('watchFeedbackForm');
      if (watchForm) {
        watchForm.dataset.createMode = 'false';
        watchForm.dataset.addMovieFormId = '';
      }
      if (reviewLaterButton) {
        reviewLaterButton.dataset.addMovieFormId = '';
      }
    }

    watchedRadio?.addEventListener('change', () => {
      resetAddMovieReviewState();
      if (watchedRadio.checked) {
        openAddMovieReviewModal();
      }
    });

    notWatchedRadio?.addEventListener('change', () => {
      resetAddMovieReviewState();
      clearAddMode();
    });

    addForm.addEventListener('submit', (event) => {
      const submitter = event.submitter || document.activeElement;
      const submitMode = submitter?.dataset?.addSubmitMode || '';
      if (submitMode === 'estimate') {
        clearAddMode();
        return;
      }

      if (!watchedRadio?.checked) {
        clearAddMode();
        return;
      }

      const hasVerdict = !!(byId('addMovieVerdict')?.value);
      const queuedForReview = String(byId('addMovieQueueForReview')?.value || '').toLowerCase() === 'true';
      if (!hasVerdict && !queuedForReview) {
        event.preventDefault();
        openAddMovieReviewModal();
      }
    });

    document.querySelectorAll('input[name="estimateWatchedStatus"]').forEach((input) => {
      input.addEventListener('change', () => {
        const value = input.value;
        const target = addForm.querySelector('input[name="Movie.WatchedStatus"][value="' + value + '"]');
        if (target) {
          closeModal(byId('estimatePreviewModal'));
          target.checked = true;
          target.dispatchEvent(new Event('change', { bubbles: true }));
        }
      });
    });
  }

  function bindEstimatePreviewModal() {
    const modal = byId('estimatePreviewModal');
    if (!modal) return;

    const autoOpen = document.querySelector('.js-auto-open-estimate-modal');
    if (autoOpen) {
      openModal(modal);
    }

    modal.querySelectorAll('.js-add-movie-submit').forEach((button) => {
      button.addEventListener('click', () => {
        closeModal(modal);
      });
    });
  }

  function bindAutoDismissStatus() {
    const toast = document.querySelector('.js-auto-dismiss-status');
    if (!toast) return;

    window.setTimeout(() => {
      toast.classList.add('is-dismissing');
      window.setTimeout(() => {
        toast.remove();
      }, 240);
    }, 2200);
  }

  document.addEventListener('DOMContentLoaded', () => {
    restoreReviewScrollPosition();
    bindSegmentedControls();
    bindReasonPickers();
    bindModalButtons();
    bindWatchFeedbackSubmit();
    bindReviewLaterSubmit();
    bindDismissSubmit();
    bindRestoreSubmit();
    bindAddMovieFlow();
    bindEstimatePreviewModal();
    bindAutoDismissStatus();
    bindSettingsPreview();

    const autoOpenWatchTrigger = document.querySelector('.js-auto-open-watch-modal');
    if (autoOpenWatchTrigger) {
      autoOpenWatchTrigger.click();
    }
  });
})();
